using ClassificadorExtractorDocumentos.Domain.Contracts;

namespace ClassificadorExtractorDocumentos.Application.Ingesta;

/// <summary>
/// Ingesta de un PDF: lo convierte a imágenes y lo guarda en disco. En E1-F1 no hay
/// Extractor/Validador todavía, así que no persiste nada en FacturasStaging (ver SPEC §E1-F1).
/// </summary>
public class IngestaDocumentoService(IPdfToImageConverter pdfToImageConverter, IDocumentStorage documentStorage)
{
    public async Task<DocumentoIngestado> IngestarAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await pdfStream.CopyToAsync(buffer, cancellationToken);
        var pdfBytes = buffer.ToArray();

        buffer.Position = 0;
        var paginasPng = await pdfToImageConverter.ConvertToPngPagesAsync(buffer, cancellationToken);

        var documentoId = Guid.NewGuid();
        var (rutaPdf, rutasImagenes) = await documentStorage.GuardarDocumentoAsync(
            documentoId, pdfBytes, paginasPng, cancellationToken);

        return new DocumentoIngestado(documentoId, rutaPdf, rutasImagenes);
    }

    public Task<IReadOnlyList<byte[]>?> ObtenerImagenesAsync(Guid documentoId, CancellationToken cancellationToken = default) =>
        documentStorage.ObtenerImagenesAsync(documentoId, cancellationToken);
}
