using ClassificadorExtractorDocumentos.Domain.Contracts;

namespace ClassificadorExtractorDocumentos.Application.Ingesta;

/// <summary>
/// Ingesta de un documento (PDF o imagen JPEG/PNG): lo convierte a páginas PNG y lo guarda en disco.
/// El formato se detecta por magic bytes; a partir de las páginas PNG el resto del pipeline es idéntico
/// sea cual sea el origen. Formato no admitido → <see cref="FormatoNoSoportadoException"/>.
/// </summary>
public class IngestaDocumentoService(
    IPdfToImageConverter pdfToImageConverter,
    IPdfTextExtractor pdfTextExtractor,
    IImagenNormalizer imagenNormalizer,
    IDocumentStorage documentStorage)
{
    public async Task<DocumentoIngestado> IngestarAsync(Stream documentoStream, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await documentoStream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        var formato = DeteccionFormato.Detectar(bytes);

        IReadOnlyList<byte[]> paginasPng;
        string? textoPdf = null;
        if (formato == FormatoDocumento.Pdf)
        {
            // Si el PDF tiene capa de texto (nacido digital), se extrae como referencia adicional
            // para el Extractor. Un PDF escaneado/fotografiado no la tiene: textoPdf queda en null
            // y el pipeline sigue solo con visión, como hasta ahora. Stream propio (a partir de los
            // bytes ya leídos): el convertidor a imagen cierra el suyo (leaveOpen: false internamente).
            textoPdf = await pdfTextExtractor.ExtraerTextoAsync(new MemoryStream(bytes), cancellationToken);

            buffer.Position = 0;
            paginasPng = await pdfToImageConverter.ConvertToPngPagesAsync(buffer, cancellationToken);
        }
        else if (formato.EsImagen())
        {
            // Una imagen es una sola "página"; se normaliza a PNG para uniformar la entrada al Extractor
            paginasPng = [await imagenNormalizer.NormalizarAPngAsync(bytes, cancellationToken)];
        }
        else
        {
            throw new FormatoNoSoportadoException();
        }

        var documentoId = Guid.NewGuid();
        var (rutaOriginal, rutasImagenes) = await documentStorage.GuardarDocumentoAsync(
            documentoId, bytes, formato.Extension(), paginasPng, cancellationToken);

        return new DocumentoIngestado(documentoId, rutaOriginal, rutasImagenes, textoPdf);
    }

    public Task<IReadOnlyList<byte[]>?> ObtenerImagenesAsync(Guid documentoId, CancellationToken cancellationToken = default) =>
        documentStorage.ObtenerImagenesAsync(documentoId, cancellationToken);
}
