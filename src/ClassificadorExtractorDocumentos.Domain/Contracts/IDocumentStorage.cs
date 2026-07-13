namespace ClassificadorExtractorDocumentos.Domain.Contracts;

public interface IDocumentStorage
{
    /// <summary>Guarda el documento original (PDF o imagen) y sus páginas renderizadas a PNG bajo una
    /// carpeta propia del documento.</summary>
    /// <param name="extensionOriginal">Extensión del original sin punto: "pdf", "jpg", "png".</param>
    /// <returns>Ruta del original guardado y rutas de cada página PNG, en orden.</returns>
    Task<(string RutaOriginal, IReadOnlyList<string> RutasImagenes)> GuardarDocumentoAsync(
        Guid documentoId,
        byte[] originalBytes,
        string extensionOriginal,
        IReadOnlyList<byte[]> paginasPng,
        CancellationToken cancellationToken = default);

    /// <summary>Recupera las páginas PNG de un documento ya ingestado, en orden. Null si el documento no existe.</summary>
    Task<IReadOnlyList<byte[]>?> ObtenerImagenesAsync(Guid documentoId, CancellationToken cancellationToken = default);
}
