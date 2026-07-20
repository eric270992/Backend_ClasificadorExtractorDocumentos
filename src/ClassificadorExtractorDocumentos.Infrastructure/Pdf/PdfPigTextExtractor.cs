using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace ClassificadorExtractorDocumentos.Infrastructure.Pdf;

/// <summary>Extrae texto vía PdfPig (.NET puro, sin dependencias nativas). Un PDF escaneado/fotografiado
/// no tiene capa de texto: PdfPig no encuentra letras y esta clase devuelve null sin más — el resto del
/// pipeline sigue funcionando solo con las imágenes, como hasta ahora.</summary>
public class PdfPigTextExtractor(ILogger<PdfPigTextExtractor> logger) : IPdfTextExtractor
{
    private const int MinimoCaracteresAprovechables = 20;

    public Task<string?> ExtraerTextoAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        try
        {
            using var documento = PdfDocument.Open(pdfStream);
            var texto = string.Join(
                "\n\n",
                documento.GetPages().Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

            return Task.FromResult(texto.Trim().Length >= MinimoCaracteresAprovechables ? texto : null);
        }
        catch (Exception ex)
        {
            // Best-effort: un PDF cifrado, corrupto o con un formato que PdfPig no soporta no debe
            // tumbar la ingesta. Sin texto extraído, el pipeline sigue solo con visión.
            logger.LogWarning(ex, "No se pudo extraer texto del PDF; se continúa solo con las imágenes.");
            return Task.FromResult<string?>(null);
        }
    }
}
