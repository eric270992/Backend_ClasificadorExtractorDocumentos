using ClassificadorExtractorDocumentos.Application.Prompts;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.Extensions.Logging;

namespace ClassificadorExtractorDocumentos.Application.Extraccion;

/// <summary>
/// Agente Extractor, nivel 3 genérico (SPEC §E1-F2): envía las páginas al LLM con el prompt
/// versionado y parsea la respuesta. JSON inválido → 1 reintento con feedback; si vuelve a
/// fallar, resultado de error controlado.
/// </summary>
public class ExtractorAgent(ILlmClient llmClient, ILogger<ExtractorAgent> logger)
{
    /// <summary>Valor de <c>FacturasStaging.NivelExtraccion</c> para extracción genérica (proveedor nuevo/
    /// desconocido, sin ejemplos few-shot). El nivel 2 (few-shot, proveedor ya conocido) llega en Etapa 2.</summary>
    public const byte NivelExtraccionGenerica = 3;

    /// <summary>El modelo multimodal de Groq admite máximo 5 imágenes por petición (límite del
    /// proveedor; verificado con qwen/qwen3.6-27b). Los datos de una factura están en las primeras
    /// páginas; documentos más largos se truncan (limitación anotada, E1-F2).</summary>
    public const int MaxPaginasPorPeticion = 5;

    private static readonly Lazy<(string Sistema, string Usuario)> Prompt =
        new(() => PromptLoader.Cargar("extraccion-generica.md"));

    /// <param name="textoPdf">Texto embebido del PDF, si lo tiene (ver <see cref="Domain.Contracts.IPdfTextExtractor"/>).
    /// Null en documentos escaneados/fotografiados: el modelo trabaja solo con las imágenes, como antes.</param>
    public async Task<ResultadoExtraccion> ExtraerAsync(
        IReadOnlyList<byte[]> paginasPng, string? textoPdf = null, CancellationToken cancellationToken = default)
    {
        if (paginasPng.Count > MaxPaginasPorPeticion)
        {
            paginasPng = paginasPng.Take(MaxPaginasPorPeticion).ToList();
        }

        var (sistema, usuarioBase) = Prompt.Value;
        var usuario = ConTextoPdf(usuarioBase, textoPdf);

        var respuesta = await llmClient.CompletarAsync(
            new LlmPeticion(sistema, usuario, paginasPng), cancellationToken);

        var parseo = FacturaExtraidaParser.Parse(respuesta);
        if (parseo.Exito)
        {
            return ResultadoExtraccion.Ok(parseo.Factura!, parseo.JsonOriginal!, reintentos: 0);
        }

        // Reintento único con feedback del error (SPEC §7: "LLM devuelve JSON malformado")
        var usuarioConFeedback =
            $"{usuario}\n\nATENCIÓN: tu respuesta anterior no cumplía el contrato ({parseo.Error}). " +
            "Devuelve ÚNICAMENTE el objeto JSON válido según el esquema, sin ningún otro texto.";

        respuesta = await llmClient.CompletarAsync(
            new LlmPeticion(sistema, usuarioConFeedback, paginasPng), cancellationToken);

        parseo = FacturaExtraidaParser.Parse(respuesta);
        if (parseo.Exito)
        {
            return ResultadoExtraccion.Ok(parseo.Factura!, parseo.JsonOriginal!, reintentos: 1);
        }

        // Sin esto, el texto crudo del LLM se pierde: solo queda el mensaje de la excepción de
        // parseo (p. ej. posición del carácter inválido), inútil para diagnosticar el problema real.
        logger.LogWarning("Extracción fallida tras 1 reintento ({Error}). Respuesta cruda del LLM: {Respuesta}",
            parseo.Error, respuesta);
        return ResultadoExtraccion.Fallo($"Extracción fallida tras 1 reintento: {parseo.Error}");
    }

    /// <summary>Si el PDF tiene texto embebido, se lo damos al modelo como referencia: leer un número
    /// en texto plano es mucho más fiable que leerlo en la imagen (ver bug de porcentajes/importes
    /// mal leídos). Las imágenes siguen enviándose igual, para la estructura visual (qué es línea,
    /// qué es cabecera/pie).</summary>
    private static string ConTextoPdf(string usuarioBase, string? textoPdf) =>
        string.IsNullOrWhiteSpace(textoPdf)
            ? usuarioBase
            : $"{usuarioBase}\n\nTexto extraído directamente del PDF (más fiable que leerlo en la imagen " +
              $"para números, porcentajes e importes — úsalo como referencia):\n---\n{textoPdf}\n---";

}

public sealed record ResultadoExtraccion(
    bool Exito,
    Domain.Contracts.FacturaExtraida? Factura,
    string? JsonOriginal,
    int Reintentos,
    string? Error)
{
    public static ResultadoExtraccion Ok(Domain.Contracts.FacturaExtraida factura, string json, int reintentos) =>
        new(true, factura, json, reintentos, null);

    public static ResultadoExtraccion Fallo(string error) => new(false, null, null, 1, error);
}
