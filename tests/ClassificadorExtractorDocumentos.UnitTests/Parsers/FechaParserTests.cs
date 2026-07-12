using ClassificadorExtractorDocumentos.Domain.Parsers;

namespace ClassificadorExtractorDocumentos.UnitTests.Parsers;

public class FechaParserTests
{
    [Theory]
    [InlineData("2026-07-05", "2026-07-05")]           // ya ISO
    [InlineData("05/07/2026", "2026-07-05")]
    [InlineData("5/7/2026", "2026-07-05")]
    [InlineData("05-07-2026", "2026-07-05")]
    [InlineData("05.07.2026", "2026-07-05")]
    [InlineData("5 julio 2026", "2026-07-05")]
    [InlineData("5 de julio de 2026", "2026-07-05")]
    [InlineData("17 juliol 2026", "2026-07-17")]
    [InlineData("1 de març de 2025", "2025-03-01")]
    [InlineData("July 5, 2026", "2026-07-05")]
    [InlineData("5 July 2026", "2026-07-05")]
    [InlineData("12 Aug 2025", "2025-08-12")]
    public void Parse_normaliza_a_iso8601(string texto, string esperado)
    {
        Assert.Equal(esperado, FechaParser.Parse(texto));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sin fecha")]
    [InlineData("32/13/2026")]      // fecha imposible
    [InlineData("5 frutero 2026")]  // mes inexistente
    public void Parse_devuelve_null_si_no_reconocible(string? texto)
    {
        Assert.Null(FechaParser.Parse(texto));
    }
}
