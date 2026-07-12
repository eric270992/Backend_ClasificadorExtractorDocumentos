namespace ClassificadorExtractorDocumentos.Domain.Contracts;

public interface IDocumentStorage
{
    /// <summary>Guarda el PDF original y sus páginas renderizadas bajo una carpeta propia del documento.</summary>
    /// <returns>Ruta del PDF guardado y rutas de cada página PNG, en orden.</returns>
    Task<(string RutaPdf, IReadOnlyList<string> RutasImagenes)> GuardarDocumentoAsync(
        Guid documentoId,
        byte[] pdfBytes,
        IReadOnlyList<byte[]> paginasPng,
        CancellationToken cancellationToken = default);

    /// <summary>Recupera las páginas PNG de un documento ya ingestado, en orden. Null si el documento no existe.</summary>
    Task<IReadOnlyList<byte[]>?> ObtenerImagenesAsync(Guid documentoId, CancellationToken cancellationToken = default);
}
