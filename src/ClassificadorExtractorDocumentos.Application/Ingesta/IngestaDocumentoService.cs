using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.Extensions.Options;

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
    IDocumentStorage documentStorage,
    IOptions<IngestaOptions> options)
{
    private readonly IngestaOptions _options = options.Value;

    public async Task<DocumentoIngestado> IngestarAsync(
        Stream documentoStream, bool? usarTextoPdf = null, CancellationToken cancellationToken = default)
    {
        // Span de tota la ingesta (lectura del fitxer, text, imatges, disc). Els spans pdf.*, imagen.*
        // i storage.* queden niats a dins i en desglossen el temps.
        using var activity = PipelineTracing.Source.StartActivity("ingesta.documento");

        var extraerTexto = usarTextoPdf ?? _options.UsarTextoPdf;

        using var buffer = new MemoryStream();
        await documentoStream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        var formato = DeteccionFormato.Detectar(bytes);
        activity?.SetTag("documento.formato", formato.ToString());
        activity?.SetTag("documento.bytes", bytes.Length);

        IReadOnlyList<byte[]> paginasPng;
        string? textoPdf = null;
        if (formato == FormatoDocumento.Pdf)
        {

            // Si el PDF tiene capa de texto (nacido digital), se extrae como referencia adicional
            // para el Extractor. Un PDF escaneado/fotografiado no la tiene: textoPdf queda en null
            // y el pipeline sigue solo con visión, como hasta ahora. Stream propio (a partir de los
            // bytes ya leídos): el convertidor a imagen cierra el suyo (leaveOpen: false internamente).
            // Desactivable con Ingesta:UsarTextoPdf=false (vuelve al modo solo-visión anterior).
            if (extraerTexto)
            {
                textoPdf = await pdfTextExtractor.ExtraerTextoAsync(new MemoryStream(bytes), cancellationToken);
            }

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

        activity?.SetTag("documento.paginas", paginasPng.Count);
        activity?.SetTag("documento.con_texto_pdf", textoPdf is not null);

        var documentoId = Guid.NewGuid();
        activity?.SetTag("documento.id", documentoId);
        var (rutaOriginal, rutasImagenes) = await documentStorage.GuardarDocumentoAsync(
            documentoId, bytes, formato.Extension(), paginasPng, cancellationToken);

        return new DocumentoIngestado(documentoId, rutaOriginal, rutasImagenes, textoPdf);
    }

    public Task<IReadOnlyList<byte[]>?> ObtenerImagenesAsync(Guid documentoId, CancellationToken cancellationToken = default) =>
        documentStorage.ObtenerImagenesAsync(documentoId, cancellationToken);
}
