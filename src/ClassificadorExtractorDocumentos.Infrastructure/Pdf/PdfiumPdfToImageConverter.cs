using ClassificadorExtractorDocumentos.Domain.Contracts;
using PDFtoImage;
using SkiaSharp;

namespace ClassificadorExtractorDocumentos.Infrastructure.Pdf;

/// <summary>Renderiza PDF a PNG vía PDFium (paquete PDFtoImage), ~150 DPI por página (SPEC §E1-F1).</summary>
public class PdfiumPdfToImageConverter : IPdfToImageConverter
{
    private const int Dpi = 150;

    public Task<IReadOnlyList<byte[]>> ConvertToPngPagesAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        var options = new RenderOptions(Dpi: Dpi);
        var pages = new List<byte[]>();

#pragma warning disable CA1416 // PDFium (via PDFtoImage) soporta Windows/Linux/macOS sin versión mínima; el analizador no lo infiere en TFM sin sufijo de SO.
        foreach (var bitmap in Conversion.ToImages(pdfStream, leaveOpen: false, password: null, options: options))
#pragma warning restore CA1416
        {
            using (bitmap)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var data = bitmap.Encode(SKEncodedImageFormat.Png, quality: 100);
                pages.Add(data.ToArray());
            }
        }

        return Task.FromResult<IReadOnlyList<byte[]>>(pages);
    }
}
