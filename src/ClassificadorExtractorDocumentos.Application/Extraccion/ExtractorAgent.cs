using ClassificadorExtractorDocumentos.Application.Prompts;
using ClassificadorExtractorDocumentos.Domain.Contracts;

namespace ClassificadorExtractorDocumentos.Application.Extraccion;

/// <summary>
/// Agente Extractor, nivel 3 genérico (SPEC §E1-F2): envía las páginas al LLM con el prompt
/// versionado y parsea la respuesta. JSON inválido → 1 reintento con feedback; si vuelve a
/// fallar, resultado de error controlado.
/// </summary>
public class ExtractorAgent(ILlmClient llmClient)
{
    /// <summary>Valor de <c>FacturasStaging.NivelExtraccion</c> para extracción genérica (proveedor nuevo/
    /// desconocido, sin ejemplos few-shot). El nivel 2 (few-shot, proveedor ya conocido) llega en Etapa 2.</summary>
    public const byte NivelExtraccionGenerica = 3;

    /// <summary>Groq/Llama 4 Scout admite máximo 5 imágenes por petición. Los datos de una factura
    /// están en las primeras páginas; documentos más largos se truncan (limitación anotada, E1-F2).</summary>
    public const int MaxPaginasPorPeticion = 5;

    private static readonly Lazy<(string Sistema, string Usuario)> Prompt =
        new(() => PromptLoader.Cargar("extraccion-generica.md"));

    public async Task<ResultadoExtraccion> ExtraerAsync(IReadOnlyList<byte[]> paginasPng, CancellationToken cancellationToken = default)
    {
        if (paginasPng.Count > MaxPaginasPorPeticion)
        {
            paginasPng = paginasPng.Take(MaxPaginasPorPeticion).ToList();
        }

        var (sistema, usuario) = Prompt.Value;

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
        return parseo.Exito
            ? ResultadoExtraccion.Ok(parseo.Factura!, parseo.JsonOriginal!, reintentos: 1)
            : ResultadoExtraccion.Fallo($"Extracción fallida tras 1 reintento: {parseo.Error}");
    }

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
