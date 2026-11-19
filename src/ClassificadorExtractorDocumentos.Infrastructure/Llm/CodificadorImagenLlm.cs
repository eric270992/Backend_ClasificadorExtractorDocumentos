using SkiaSharp;

namespace ClassificadorExtractorDocumentos.Infrastructure.Llm;

/// <summary>
/// Prepara una imatge PNG per enviar-la al model: la deixa tal qual o la recodifica a JPEG.
/// Viu a Infrastructure/Llm perquè és un detall del transport cap al proveïdor LLM, no del pipeline:
/// la resta de l'aplicació (disc, frontend, Extractor) continua treballant amb PNG.
/// </summary>
internal static class CodificadorImagenLlm
{
    public static (string MimeType, byte[] Bytes) Preparar(byte[] png, string formato, int calidadJpeg)
    {
        if (!formato.Equals("jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return ("image/png", png);
        }

        using var original = SKImage.FromEncodedData(png)
            ?? throw new InvalidOperationException("No s'ha pogut decodificar la imatge per enviar-la al model.");

        // JPEG no té canal alfa: una zona transparent (p. ex. un PNG pujat amb fons transparent) es
        // convertiria en NEGRE i el text negre desapareixeria. Per això es pinta primer sobre blanc.
        var info = new SKImageInfo(original.Width, original.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawImage(original, 0, 0);

        using var resultat = surface.Snapshot();
        using var data = resultat.Encode(SKEncodedImageFormat.Jpeg, Math.Clamp(calidadJpeg, 1, 100));
        return ("image/jpeg", data.ToArray());
    }
}
