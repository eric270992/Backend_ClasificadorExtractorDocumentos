using System.Diagnostics;
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
        using var activity = PdfTracing.Source.StartActivity("pdf.extract_text");
        try
        {
            using var documento = PdfDocument.Open(pdfStream);
            var texto = string.Join(
                "\n\n",
                documento.GetPages().Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

            var aprovechable = texto.Trim().Length >= MinimoCaracteresAprovechables;
            activity?.SetTag("pdf.text.length", texto.Trim().Length);
            activity?.SetTag("pdf.text.found", aprovechable);
            return Task.FromResult(aprovechable ? texto : null);
        }
        catch (Exception ex)
        {
            activity?.SetTag("pdf.text.found", false);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // Best-effort: un PDF cifrado, corrupto o con un formato que PdfPig no soporta no debe
            // tumbar la ingesta. Sin texto extraído, el pipeline sigue solo con visión.
            logger.LogWarning(ex, "No se pudo extraer texto del PDF; se continúa solo con las imágenes.");
            return Task.FromResult<string?>(null);
        }
    }
}
