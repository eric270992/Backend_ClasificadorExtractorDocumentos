using ClassificadorExtractorDocumentos.Application.Ingesta;
using ClassificadorExtractorDocumentos.Domain.Contracts;

namespace ClassificadorExtractorDocumentos.UnitTests.Ingesta;

public class IngestaDocumentoServiceTests
{
    private static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46, 0x00];
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    [Fact]
    public async Task Ingestar_pdf_con_texto_lo_incluye_en_el_resultado()
    {
        var servicio = new IngestaDocumentoService(
            new ConversorFake(), new TextExtractorFake("IVA (21%): 89,25"), new NormalizerFake(), new StorageFake());

        var documento = await servicio.IngestarAsync(new MemoryStream(PdfBytes));

        Assert.Equal("IVA (21%): 89,25", documento.TextoPdf);
    }

    [Fact]
    public async Task Ingestar_pdf_sin_capa_de_texto_deja_TextoPdf_en_null()
    {
        var servicio = new IngestaDocumentoService(
            new ConversorFake(), new TextExtractorFake(null), new NormalizerFake(), new StorageFake());

        var documento = await servicio.IngestarAsync(new MemoryStream(PdfBytes));

        Assert.Null(documento.TextoPdf);
    }

    [Fact]
    public async Task Ingestar_una_imagen_no_extrae_texto()
    {
        var extractor = new TextExtractorFake("esto no debería usarse");
        var servicio = new IngestaDocumentoService(
            new ConversorFake(), extractor, new NormalizerFake(), new StorageFake());

        var documento = await servicio.IngestarAsync(new MemoryStream(PngBytes));

        Assert.Null(documento.TextoPdf);
        Assert.Equal(0, extractor.Llamadas);
    }

    private sealed class ConversorFake : IPdfToImageConverter
    {
        public Task<IReadOnlyList<byte[]>> ConvertToPngPagesAsync(Stream pdfStream, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<byte[]>>([[1, 2, 3]]);
    }

    private sealed class TextExtractorFake(string? texto) : IPdfTextExtractor
    {
        public int Llamadas { get; private set; }

        public Task<string?> ExtraerTextoAsync(Stream pdfStream, CancellationToken ct = default)
        {
            Llamadas++;
            return Task.FromResult(texto);
        }
    }

    private sealed class NormalizerFake : IImagenNormalizer
    {
        public Task<byte[]> NormalizarAPngAsync(byte[] imagen, CancellationToken ct = default) =>
            Task.FromResult<byte[]>([1, 2, 3]);
    }

    private sealed class StorageFake : IDocumentStorage
    {
        public Task<(string, IReadOnlyList<string>)> GuardarDocumentoAsync(
            Guid id, byte[] original, string extensionOriginal, IReadOnlyList<byte[]> paginas, CancellationToken ct = default) =>
            Task.FromResult(($"mem://{id}/original.{extensionOriginal}", (IReadOnlyList<string>)[$"mem://{id}/page-1.png"]));

        public Task<IReadOnlyList<byte[]>?> ObtenerImagenesAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<byte[]>?>([[1, 2, 3]]);
    }
}
