using ClassificadorExtractorDocumentos.Domain.Grounding;

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

    /// <summary>Paraules de cada pàgina amb la seva caixa normalitzada (0-1, origen a dalt a l'esquerra),
    /// per localitzar on surt cada valor extret (grounding híbrid). Null si el PDF no té capa de text:
    /// en aquest cas l'única manera de localitzar els camps és demanar-ho al model de visió.</summary>
    Task<IReadOnlyList<PaginaPalabras>?> ExtraerPalabrasAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}
