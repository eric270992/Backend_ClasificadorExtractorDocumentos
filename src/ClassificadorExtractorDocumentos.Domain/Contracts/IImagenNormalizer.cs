namespace ClassificadorExtractorDocumentos.Domain.Contracts;

/// <summary>
/// Normaliza una imagen (JPEG/PNG) a PNG, para que el resto del pipeline reciba siempre PNG igual que
/// las páginas renderizadas de un PDF. Así el Extractor y el cliente LLM no distinguen el origen.
/// </summary>
public interface IImagenNormalizer
{
    Task<byte[]> NormalizarAPngAsync(byte[] imagen, CancellationToken cancellationToken = default);
}
