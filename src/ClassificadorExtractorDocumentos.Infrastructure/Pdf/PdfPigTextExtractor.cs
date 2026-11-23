using System.Diagnostics;
using System.Text;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Grounding;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ClassificadorExtractorDocumentos.Infrastructure.Pdf;

/// <summary>Extrae texto vía PdfPig (.NET puro, sin dependencias nativas). Un PDF escaneado/fotografiado
/// no tiene capa de texto: PdfPig no encuentra letras y esta clase devuelve null sin más — el resto del
/// pipeline sigue funcionando solo con las imágenes, como hasta ahora.
/// <para>
/// IMPORTANTE: NO se usa <c>Page.Text</c>. Esa propiedad concatena las letras en el orden del flujo de
/// contenido del PDF, sin espacios ni saltos de línea. En PDFs generados por Crystal Reports (p. ej. COEVA)
/// ese orden es "cantidad total precio DESCRIPCIÓN qpvp dte REFERENCIA", de modo que los números de una
/// línea quedan pegados a la descripción de la línea SIGUIENTE y columnas como Q.Pvp+Dte se fusionan
/// ("1" + "74,0" → "174,0"). El modelo confiaba en ese texto y desplazaba importes una fila.
/// Aquí se reconstruye el texto respetando la maqueta (como <c>pdftotext -layout</c>): palabras agrupadas
/// por fila según su coordenada Y y ordenadas de izquierda a derecha, con espacios proporcionales a la
/// distancia horizontal para que las columnas queden alineadas.
/// </para></summary>
public class PdfPigTextExtractor(ILogger<PdfPigTextExtractor> logger) : IPdfTextExtractor
{
    private const int MinimoCaracteresAprovechables = 20;

    /// <summary>Puntos PDF que equivalen a una "columna" de texto plano. Con ~4 pt, una línea A4
    /// (595 pt) ocupa ~150 caracteres: suficiente resolución para separar columnas sin inflar el prompt.</summary>
    private const double PuntosPorColumna = 4.0;

    /// <summary>Dos palabras están en la misma fila si sus líneas base difieren menos que esta fracción
    /// de la altura de letra. Absorbe pequeños desajustes verticales entre campos de un mismo informe.</summary>
    private const double ToleranciaFilaRelativaAltura = 0.5;

    /// <summary>Si el hueco entre dos palabras consecutivas de una fila es menor que esta fracción de la
    /// altura de letra, son palabras de una misma frase y se separan con un único espacio. Sin esto, el
    /// redondeo a columnas de <see cref="PuntosPorColumna"/> metía 2-4 espacios dentro de una descripción
    /// ("ELECTRONIC    REX12"). Solo los huecos mayores (cambio de columna) se rellenan hasta su columna.</summary>
    private const double HuecoMaximoEntrePalabrasDeFraseRelativoAltura = 0.8;

    public Task<string?> ExtraerTextoAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        using var activity = PdfTracing.Source.StartActivity("pdf.extract_text");
        try
        {
            using var documento = PdfDocument.Open(pdfStream);

            // Se marca el inicio de cada página: así el modelo sabe que una tabla continúa en la página
            // siguiente (las cabeceras se repiten) y no da por terminada la lista de líneas en la página 1.
            var paginas = documento.GetPages()
                .Select(pagina => (Numero: pagina.Number, Texto: ReconstruirTextoConMaqueta(pagina)))
                .Where(pagina => !string.IsNullOrWhiteSpace(pagina.Texto))
                .Select(pagina => $"[Página {pagina.Numero}]\n{pagina.Texto}");

            var texto = string.Join("\n\n", paginas);

            var aprovechable = texto.Trim().Length >= MinimoCaracteresAprovechables;
            activity?.SetTag("pdf.text.length", texto.Trim().Length);
            activity?.SetTag("pdf.text.found", aprovechable);
            return Task.FromResult(aprovechable ? texto : null);
        }
        catch (Exception ex)
        {
            activity?.SetTag("pdf.text.found", false);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // Best-effort: un PDF cifrado, corrupto o con un formato que PdfPig no soporta no debe
            // tumbar la ingesta. Sin texto extraído, el pipeline sigue solo con visión.
            logger.LogWarning(ex, "No se pudo extraer texto del PDF; se continúa solo con las imágenes.");
            return Task.FromResult<string?>(null);
        }
    }

    public Task<IReadOnlyList<PaginaPalabras>?> ExtraerPalabrasAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        using var activity = PdfTracing.Source.StartActivity("pdf.extract_words");
        try
        {
            using var documento = PdfDocument.Open(pdfStream);

            var paginas = new List<PaginaPalabras>();
            var totalPalabras = 0;
            foreach (var pagina in documento.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var palabras = pagina.GetWords()
                    .Where(palabra => !string.IsNullOrWhiteSpace(palabra.Text))
                    .Select(palabra => new PalabraDocumento(palabra.Text, NormalizarCaja(pagina, palabra)))
                    .ToList();

                totalPalabras += palabras.Count;
                paginas.Add(new PaginaPalabras(pagina.Number, pagina.Rotation.Value, palabras));
            }

            activity?.SetTag("pdf.words.count", totalPalabras);

            // Mateix criteri que ExtraerTextoAsync: sense paraules, el PDF és escanejat i no hi ha capa de text
            return Task.FromResult<IReadOnlyList<PaginaPalabras>?>(totalPalabras == 0 ? null : paginas);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // Best-effort, igual que amb el text: el grounding és una ajuda visual, no pot fer caure res
            logger.LogWarning(ex, "No se pudieron extraer las palabras del PDF para el grounding.");
            return Task.FromResult<IReadOnlyList<PaginaPalabras>?>(null);
        }
    }

    /// <summary>Passa la caixa d'una paraula de punts PDF a coordenades 0-1 de la imatge renderitzada.
    /// <para>
    /// Dues diferències entre el sistema de coordenades del PDF i el de la imatge:
    /// 1) Al PDF la Y creix cap amunt (origen a baix a l'esquerra); a la imatge creix cap avall. Per això
    ///    la Y de dalt de la imatge surt de la vora SUPERIOR de la paraula (<c>Top</c>) i s'inverteix.
    /// 2) PDFium renderitza la CropBox (la zona visible), que pot no començar a (0,0). Restem el seu origen
    ///    i dividim per la seva mida perquè la caixa quadri amb la imatge.
    /// </para>
    /// LIMITACIÓ coneguda (prova): no es tenen en compte les pàgines amb rotació (/Rotate 90/180/270);
    /// en aquests casos les caixes sortirien girades. El servei ho avisa a la resposta.</summary>
    private static CajaNormalizada NormalizarCaja(Page pagina, Word palabra)
    {
        var zonaVisible = pagina.CropBox.Bounds;
        var ancho = Math.Max(zonaVisible.Width, 1.0);
        var alto = Math.Max(zonaVisible.Height, 1.0);

        var caja = palabra.BoundingBox;
        return CajaNormalizada.Crear(
            x1: (caja.Left - zonaVisible.Left) / ancho,
            y1: 1.0 - (caja.Top - zonaVisible.Bottom) / alto,
            x2: (caja.Right - zonaVisible.Left) / ancho,
            y2: 1.0 - (caja.Bottom - zonaVisible.Bottom) / alto);
    }

    /// <summary>Reconstruye el texto de una página fila a fila y columna a columna.</summary>
    private static string ReconstruirTextoConMaqueta(Page pagina)
    {
        // 1) Palabras de la página (PdfPig ya agrupa letras en palabras usando su posición, no el flujo).
        var palabras = pagina.GetWords()
            .Where(palabra => !string.IsNullOrWhiteSpace(palabra.Text))
            .ToList();
        if (palabras.Count == 0)
        {
            return string.Empty;
        }

        // 2) Agrupar en filas. En PDF la Y crece hacia arriba, así que se ordena de mayor a menor Y
        //    (de arriba abajo). Una palabra se añade a la fila actual si su línea base está "cerca".
        var palabrasDeArribaAAbajo = palabras
            .OrderByDescending(palabra => palabra.BoundingBox.Bottom)
            .ThenBy(palabra => palabra.BoundingBox.Left);

        var filas = new List<List<Word>>();
        double? baseFilaActual = null;
        foreach (var palabra in palabrasDeArribaAAbajo)
        {
            var alturaLetra = Math.Max(palabra.BoundingBox.Height, 1.0);
            var tolerancia = alturaLetra * ToleranciaFilaRelativaAltura;

            if (baseFilaActual is null || Math.Abs(baseFilaActual.Value - palabra.BoundingBox.Bottom) > tolerancia)
            {
                filas.Add([]);
                baseFilaActual = palabra.BoundingBox.Bottom;
            }

            filas[^1].Add(palabra);
        }

        // 3) Cada fila: palabras de izquierda a derecha, colocadas en la columna que les toca según su X.
        var resultado = new StringBuilder();
        foreach (var fila in filas)
        {
            var linea = new StringBuilder();
            Word? anterior = null;
            foreach (var palabra in fila.OrderBy(palabraFila => palabraFila.BoundingBox.Left))
            {
                var columnaDestino = (int)Math.Round(palabra.BoundingBox.Left / PuntosPorColumna);

                int espacios;
                if (anterior is null)
                {
                    espacios = columnaDestino;
                }
                else
                {
                    var hueco = palabra.BoundingBox.Left - anterior.BoundingBox.Right;
                    var alturaLetra = Math.Max(palabra.BoundingBox.Height, 1.0);

                    // Al menos un espacio entre palabras, aunque la columna calculada ya esté ocupada
                    // (texto más ancho de lo que estima PuntosPorColumna): nunca se pegan dos campos.
                    espacios = hueco <= alturaLetra * HuecoMaximoEntrePalabrasDeFraseRelativoAltura
                        ? 1
                        : Math.Max(columnaDestino - linea.Length, 1);
                }

                linea.Append(' ', espacios);
                linea.Append(palabra.Text);
                anterior = palabra;
            }

            resultado.AppendLine(linea.ToString().TrimEnd());
        }

        return resultado.ToString().TrimEnd();
    }
}
