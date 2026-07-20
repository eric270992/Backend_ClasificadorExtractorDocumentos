namespace ClassificadorExtractorDocumentos.Domain.Contracts;

/// <summary>
/// Extrae el texto embebido de un PDF "nacido digital" (con capa de texto real), para dárselo al
/// LLM como referencia junto a las imágenes: leer un número en texto plano es mucho más fiable para
/// el modelo que leerlo visualmente. Una factura escaneada o fotografiada no tiene capa de texto;
/// en ese caso no hay nada que extraer y el pipeline sigue solo con visión, como hasta ahora.
/// </summary>
public interface IPdfTextExtractor
{
    /// <summary>Null si el PDF no tiene texto aprovechable (escaneado/fotografiado, o vacío).</summary>
    Task<string?> ExtraerTextoAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}
