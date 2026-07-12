using ClassificadorExtractorDocumentos.Domain.Parsers;

namespace ClassificadorExtractorDocumentos.UnitTests.Parsers;

public class NumeroParserTests
{
    [Theory]
    [InlineData("1.234,56 €", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("1234,56", 1234.56)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("1.234", 1234)]        // punto de miles sin decimales
    [InlineData("1,234", 1234)]        // coma de miles sin decimales
    [InlineData("12.345.678,90", 12345678.90)]
    [InlineData("€ 99,9", 99.9)]
    [InlineData("-45,20", -45.20)]
    [InlineData("0", 0)]
    [InlineData("21", 21)]
    [InlineData("150 EUR", 150)]
    public void Parse_normaliza_formatos_es_y_en(string texto, decimal esperado)
    {
        Assert.Equal(esperado, NumeroParser.Parse(texto));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("N/A")]
    [InlineData("abc")]
    public void Parse_devuelve_null_si_no_es_numero(string? texto)
    {
        Assert.Null(NumeroParser.Parse(texto));
    }
}
