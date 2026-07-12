namespace ClassificadorExtractorDocumentos.Domain.Contracts;

public interface IPdfToImageConverter
{
    /// <summary>Renderiza cada página del PDF a PNG. Una imagen por página, en orden.</summary>
    Task<IReadOnlyList<byte[]>> ConvertToPngPagesAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}
