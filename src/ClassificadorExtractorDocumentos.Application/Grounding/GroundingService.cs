using System.Diagnostics;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Entities;

namespace ClassificadorExtractorDocumentos.Application.Grounding;

/// <summary>
/// Servei de grounding (prova): diu d'on ha sortit cada dada d'una factura dins el document original, amb dos
/// mètodes intercanviables per poder-los comparar sobre la mateixa factura:
/// - "hibrido": valors ja extrets + paraules amb coordenades de la capa de text del PDF (<see cref="LocalizadorHibrido"/>).
/// - "llm": el model de visió marca la caixa de cada valor sobre les imatges (<see cref="GroundingLlmAgent"/>).
/// <para>
/// Es calcula SOTA DEMANDA (quan s'obre el visor) i no es desa a la BD: és una prova, i així no cal cap migració.
/// Si la idea es consolida, el pas natural és calcular l'híbrid durant la ingesta i desar les caixes.
/// </para>
/// Controller → aquest servei → repositori/emmagatzematge. El controlador no sap res de PDFs ni de models.
/// </summary>
public class GroundingService(
    IFacturaStagingRepository repositorio,
    IDocumentStorage documentStorage,
    IPdfTextExtractor pdfTextExtractor,
    GroundingLlmAgent groundingLlmAgent)
{
    public const string MetodoHibrido = "hibrido";
    public const string MetodoLlm = "llm";

    public static bool EsMetodoValido(string metodo) => metodo is MetodoHibrido or MetodoLlm;

    /// <summary>Localitza els camps de la factura amb el mètode indicat. Null si la factura no existeix.</summary>
    public async Task<ResultadoGrounding?> LocalizarAsync(int facturaId, string metodo, CancellationToken cancellationToken = default)
    {
        // Es reutilitza la lectura de l'edició (factura + proveïdor + línies) en lloc de crear-ne una de nova al
        // repositori: porta just el que cal. No es desa res, així que el seguiment de canvis d'EF no té efecte.
        var factura = await repositorio.ObtenerParaEdicionAsync(facturaId, cancellationToken);
        if (factura is null || ObtenerDocumentoId(factura) is not { } documentoId)
        {
            return null;
        }

        var cronometro = Stopwatch.StartNew();
        var campos = CamposDeFactura.Construir(factura);
        var numPaginas = await documentStorage.ContarPaginasAsync(documentoId, cancellationToken);
        var avisos = new List<string>();

        var localizados = metodo == MetodoLlm
            ? await LocalizarConLlmAsync(documentoId, campos, numPaginas, avisos, cancellationToken)
            : await LocalizarHibridoAsync(documentoId, campos, avisos, cancellationToken);

        cronometro.Stop();
        return new ResultadoGrounding(facturaId, metodo, numPaginas, localizados, cronometro.ElapsedMilliseconds, avisos);
    }

    /// <summary>Imatge PNG d'una pàgina del document de la factura (1 = primera), per al visor. Null si no existeix.</summary>
    public async Task<byte[]?> ObtenerImagenPaginaAsync(int facturaId, int pagina, CancellationToken cancellationToken = default)
    {
        var factura = await repositorio.ObtenerParaEdicionAsync(facturaId, cancellationToken);
        if (factura is null || ObtenerDocumentoId(factura) is not { } documentoId)
        {
            return null;
        }
        return await documentStorage.ObtenerImagenPaginaAsync(documentoId, pagina, cancellationToken);
    }

    private async Task<IReadOnlyList<CampoLocalizado>> LocalizarHibridoAsync(
        Guid documentoId, IReadOnlyList<CampoBuscado> campos, List<string> avisos, CancellationToken cancellationToken)
    {
        var original = await documentStorage.ObtenerOriginalAsync(documentoId, cancellationToken);
        if (original is null || original.Extension != "pdf")
        {
            avisos.Add("El document original és una imatge: el mètode híbrid necessita un PDF amb capa de text. Prova el mètode LLM.");
            return SinLocalizar(campos);
        }

        using var stream = new MemoryStream(original.Bytes);
        var paginas = await pdfTextExtractor.ExtraerPalabrasAsync(stream, cancellationToken);
        if (paginas is null)
        {
            avisos.Add("El PDF no té capa de text (escanejat o fotografiat): el mètode híbrid no hi pot localitzar res. Prova el mètode LLM.");
            return SinLocalizar(campos);
        }

        foreach (var pagina in paginas.Where(pagina => pagina.RotacionGrados != 0))
        {
            avisos.Add($"La pàgina {pagina.Pagina} està girada {pagina.RotacionGrados}°: les caixes d'aquesta pàgina poden sortir desplaçades (limitació de la prova).");
        }

        return LocalizadorHibrido.Localizar(paginas, campos);
    }

    private async Task<IReadOnlyList<CampoLocalizado>> LocalizarConLlmAsync(
        Guid documentoId, IReadOnlyList<CampoBuscado> campos, int numPaginas, List<string> avisos, CancellationToken cancellationToken)
    {
        var imagenes = await documentStorage.ObtenerImagenesAsync(documentoId, cancellationToken);
        if (imagenes is null || imagenes.Count == 0)
        {
            avisos.Add("No s'han trobat les imatges del document.");
            return SinLocalizar(campos);
        }

        if (numPaginas > Extraccion.ExtractorAgent.MaxPaginasPorPeticion)
        {
            avisos.Add($"Al model només se li envien les primeres {Extraccion.ExtractorAgent.MaxPaginasPorPeticion} pàgines.");
        }

        return await groundingLlmAgent.LocalizarAsync(imagenes, campos, cancellationToken);
    }

    private static IReadOnlyList<CampoLocalizado> SinLocalizar(IReadOnlyList<CampoBuscado> campos) =>
        campos.Select(campo => new CampoLocalizado(campo.Campo, campo.Etiqueta, campo.ValorMostrado,
            Pagina: null, Caja: null, Puntuacion: null, NumCandidatos: null)).ToList();

    /// <summary>
    /// La factura no guarda l'id del document, però sí la ruta de l'original, que és "{BasePath}/{documentoId}/original.pdf"
    /// (així ho desa FileSystemDocumentStorage). El nom de la carpeta és l'id. Null si la ruta no té aquesta forma.
    /// <para>Si la idea es consolida, el correcte seria afegir una columna DocumentoId a FacturasStaging.</para>
    /// </summary>
    private static Guid? ObtenerDocumentoId(FacturaStaging factura)
    {
        // Es parteix pels dos separadors perquè la ruta pot ser de Windows ('\', desenvolupament) o de Linux ('/',
        // Docker). Path.GetDirectoryName només entén el separador del sistema on s'executa.
        var partes = factura.RutaPdfOriginal.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var carpeta = partes.Length >= 2 ? partes[^2] : null;
        return Guid.TryParse(carpeta, out var documentoId) ? documentoId : null;
    }
}
