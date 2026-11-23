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

    /// <summary>Una sola pàgina PNG (1 = primera). Per al visor del frontend: llegir-les totes per servir-ne
    /// una seria malbaratar disc i memòria. Null si el document o la pàgina no existeixen.</summary>
    Task<byte[]?> ObtenerImagenPaginaAsync(Guid documentoId, int pagina, CancellationToken cancellationToken = default);

    /// <summary>Quantes pàgines PNG té el document desades a disc (0 si no existeix).</summary>
    Task<int> ContarPaginasAsync(Guid documentoId, CancellationToken cancellationToken = default);

    /// <summary>El fitxer original tal com es va pujar (PDF o imatge) amb la seva extensió sense punt.
    /// El grounding híbrid el necessita per llegir la capa de text del PDF. Null si no existeix.</summary>
    Task<DocumentoOriginal?> ObtenerOriginalAsync(Guid documentoId, CancellationToken cancellationToken = default);
}

/// <param name="Bytes">Contingut del fitxer original.</param>
/// <param name="Extension">"pdf", "jpg", "png"... en minúscules i sense punt.</param>
public sealed record DocumentoOriginal(byte[] Bytes, string Extension);
