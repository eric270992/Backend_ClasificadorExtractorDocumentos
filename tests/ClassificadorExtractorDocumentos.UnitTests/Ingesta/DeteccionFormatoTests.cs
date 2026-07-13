using ClassificadorExtractorDocumentos.Application.Ingesta;

namespace ClassificadorExtractorDocumentos.UnitTests.Ingesta;

public class DeteccionFormatoTests
{
    [Fact]
    public void Detecta_pdf_por_magic_bytes()
    {
        byte[] pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37]; // %PDF-1.7
        Assert.Equal(FormatoDocumento.Pdf, DeteccionFormato.Detectar(pdf));
    }

    [Fact]
    public void Detecta_jpeg_por_magic_bytes()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
        Assert.Equal(FormatoDocumento.Jpeg, DeteccionFormato.Detectar(jpeg));
        Assert.True(DeteccionFormato.Detectar(jpeg).EsImagen());
    }

    [Fact]
    public void Detecta_png_por_magic_bytes()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.Equal(FormatoDocumento.Png, DeteccionFormato.Detectar(png));
        Assert.True(DeteccionFormato.Detectar(png).EsImagen());
    }

    [Theory]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04 })] // ZIP/Office
    [InlineData(new byte[] { 0x01 })]                    // demasiado corto
    [InlineData(new byte[] { })]                          // vacío
    public void Formato_no_reconocido_es_desconocido(byte[] bytes)
    {
        Assert.Equal(FormatoDocumento.Desconocido, DeteccionFormato.Detectar(bytes));
    }

    [Theory]
    [InlineData(FormatoDocumento.Pdf, "pdf")]
    [InlineData(FormatoDocumento.Jpeg, "jpg")]
    [InlineData(FormatoDocumento.Png, "png")]
    public void Extension_correcta_por_formato(FormatoDocumento formato, string esperada)
    {
        Assert.Equal(esperada, formato.Extension());
    }
}
