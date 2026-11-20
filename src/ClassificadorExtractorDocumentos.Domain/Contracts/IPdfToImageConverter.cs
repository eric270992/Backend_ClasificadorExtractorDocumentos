namespace ClassificadorExtractorDocumentos.Domain.Contracts;

public interface IPdfToImageConverter
{
    /// <summary>Renderiza cada página del PDF a PNG. Una imagen por página, en orden.</summary>
    Task<IReadOnlyList<byte[]>> ConvertToPngPagesAsync(Stream pdfStream, CancellationToken cancellationToken = default);

    /// <summary>Nombre de pàgines del PDF, sense renderitzar-les (és molt ràpid). Serveix per rebutjar
    /// documents massa llargs abans de fer la feina cara.</summary>
    int ContarPaginas(byte[] pdf);
}
