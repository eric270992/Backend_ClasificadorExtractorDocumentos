using ClassificadorExtractorDocumentos.Domain.Contracts;
using SkiaSharp;

namespace ClassificadorExtractorDocumentos.Infrastructure.Imagen;

/// <summary>
/// Normalitza imatges (JPEG/PNG) a PNG amb SkiaSharp (ja present via PDFtoImage), perquè l'Extractor
/// rebi el mateix que amb les pàgines d'un PDF. Pensat també per a fotos de mòbil (tiquets):
/// <list type="bullet">
/// <item>Aplica l'orientació EXIF: el mòbil desa la foto "de costat" i indica el gir en una etiqueta.
/// Si no s'aplica, el model rep el tiquet girat 90° i la lectura empitjora molt.</item>
/// <item>Redueix les fotos molt grans (12-50 MP) a un màxim de píxels: el model no en guanya detall
/// útil, però sí molts tokens d'imatge i temps (i alguns servidors rebutgen imatges massa grans).</item>
/// <item>Aplana la transparència sobre fons blanc.</item>
/// </list>
/// </summary>
public class SkiaImagenNormalizer : IImagenNormalizer
{
    /// <summary>Límit de píxels totals (~4 MP). Es limita l'àrea i no el costat llarg perquè un tiquet
    /// és molt llarg i estret: limitar el costat llarg a, p. ex., 2000 px el deixaria a ~500 px d'ample i
    /// la lletra petita seria il·legible. 4 MP és ~2 vegades una pàgina A4 renderitzada a 150 DPI.</summary>
    private const long MaxPixeles = 4_000_000;

    public Task<byte[]> NormalizarAPngAsync(byte[] imagen, CancellationToken cancellationToken = default)
    {
        using var data = SKData.CreateCopy(imagen);
        using var codec = SKCodec.Create(data)
            ?? throw new InvalidOperationException("La imagen no se ha podido decodificar.");
        using var bitmap = SKBitmap.Decode(codec)
            ?? throw new InvalidOperationException("La imagen no se ha podido decodificar.");
        using var imagenOrigen = SKImage.FromBitmap(bitmap);

        var origen = codec.EncodedOrigin;
        int ancho = bitmap.Width, alto = bitmap.Height;

        // Les orientacions "Left*/Right*" són girs de 90°: amplada i alçada s'intercanvien
        var giraNoventa = origen is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int anchoFinal = giraNoventa ? alto : ancho;
        int altoFinal = giraNoventa ? ancho : alto;

        // Factor d'escala (només reduir, mai ampliar) per no superar MaxPixeles
        var escala = Math.Min(1.0, Math.Sqrt((double)MaxPixeles / ((long)anchoFinal * altoFinal)));
        var anchoDestino = Math.Max(1, (int)Math.Round(anchoFinal * escala));
        var altoDestino = Math.Max(1, (int)Math.Round(altoFinal * escala));

        using var surface = SKSurface.Create(
            new SKImageInfo(anchoDestino, altoDestino, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        // Ordre de les transformacions: Skia aplica al punt primer l'última que s'ha afegit.
        // Per tant: es dibuixa la imatge original → s'orienta → s'escala.
        canvas.Scale((float)escala, (float)escala);
        AplicarOrientacion(canvas, origen, ancho, alto);

        // Mitchell: bon filtre per reduir fotos sense que el text quedi borrós ni amb dents de serra
        canvas.DrawImage(imagenOrigen, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell));

        using var resultado = surface.Snapshot();
        using var png = resultado.Encode(SKEncodedImageFormat.Png, 100);
        return Task.FromResult(png.ToArray());
    }

    /// <summary>
    /// Transforma el llenç perquè la imatge, dibuixada a (0,0) amb la mida original (ancho×alto), quedi
    /// orientada com la veuria una persona. Cada cas correspon a un valor de l'etiqueta EXIF Orientation.
    /// Recordatori: RotateDegrees(90) porta (x,y) → (-y,x), és a dir, gir horari (eix Y cap avall).
    /// </summary>
    private static void AplicarOrientacion(SKCanvas canvas, SKEncodedOrigin origen, int ancho, int alto)
    {
        switch (origen)
        {
            case SKEncodedOrigin.TopRight:     // 2: mirall horitzontal
                canvas.Translate(ancho, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:  // 3: girada 180°
                canvas.Translate(ancho, alto);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft:   // 4: mirall vertical
                canvas.Translate(0, alto);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:      // 5: transposada (x,y) → (y,x)
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:     // 6: cal girar 90° horari (el cas típic d'una foto vertical de mòbil)
                canvas.Translate(alto, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:  // 7: transversa (x,y) → (alto-y, ancho-x)
                canvas.Translate(alto, ancho);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom:   // 8: cal girar 90° antihorari
                canvas.Translate(0, ancho);
                canvas.RotateDegrees(-90);
                break;
            default:                           // 1 (TopLeft): ja està ben orientada
                break;
        }
    }
}
