using ClassificadorExtractorDocumentos.Domain.Contracts;
using SkiaSharp;

namespace ClassificadorExtractorDocumentos.Infrastructure.Imagen;

/// <summary>Normaliza imágenes a PNG con SkiaSharp (ya presente vía PDFtoImage). Decodifica el
/// formato de entrada (JPEG/PNG) y reencoda a PNG, para uniformar la entrada al Extractor.</summary>
public class SkiaImagenNormalizer : IImagenNormalizer
{
    public Task<byte[]> NormalizarAPngAsync(byte[] imagen, CancellationToken cancellationToken = default)
    {
        using var bitmap = SKBitmap.Decode(imagen)
            ?? throw new InvalidOperationException("La imagen no se ha podido decodificar.");
        using var skImage = SKImage.FromBitmap(bitmap);
        using var data = skImage.Encode(SKEncodedImageFormat.Png, quality: 100);
        return Task.FromResult(data.ToArray());
    }
}
