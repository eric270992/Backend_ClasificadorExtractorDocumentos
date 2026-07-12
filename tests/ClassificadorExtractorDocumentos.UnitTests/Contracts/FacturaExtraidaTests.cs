using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.ValueObjects;
using ClassificadorExtractorDocumentos.UnitTests.Validacion;

namespace ClassificadorExtractorDocumentos.UnitTests.Contracts;

/// <summary>El modelo responde preguntas: estos tests fijan el conocimiento de dominio que
/// antes vivía disperso en las reglas de validación.</summary>
public class FacturaExtraidaTests
{
    [Fact]
    public void SumaLineas_suma_los_importes()
    {
        Assert.Equal(250m, FacturaDePrueba.Valida().SumaLineas());
    }

    [Fact]
    public void SumaLineas_es_null_si_falta_algun_importe()
    {
        var factura = FacturaDePrueba.Valida() with
        {
            Lineas = [new LineaExtraida("X", 1, 10m, 21m, null)],
        };

        Assert.Null(factura.SumaLineas());
    }

    [Fact]
    public void ImporteObjetivoLineas_es_la_base_o_base_mas_iva_segun_flag()
    {
        var factura = FacturaDePrueba.Valida();

        Assert.Equal(250m, factura.ImporteObjetivoLineas());
        Assert.Equal(302.5m, (factura with { LineasIncluyenIva = true }).ImporteObjetivoLineas());
    }

    [Fact]
    public void CuotaIvaCalculadaPorLineas_deriva_la_base_cuando_el_importe_incluye_iva()
    {
        // 121 con IVA incluido al 21% → base 100 → cuota 21
        var factura = FacturaDePrueba.Valida() with
        {
            LineasIncluyenIva = true,
            Lineas = [new LineaExtraida("X", 1, 100m, 21m, 121m)],
        };

        Assert.Equal(21m, Math.Round(factura.CuotaIvaCalculadaPorLineas()!.Value, 2));
    }

    [Fact]
    public void TotalCalculado_es_base_mas_iva_menos_irpf()
    {
        var totales = new TotalesExtraidos(250m, 52.5m, 37.5m, 265m);

        Assert.Equal(265m, totales.TotalCalculado);
    }

    [Fact]
    public void TotalCalculado_es_null_si_falta_base_o_cuota()
    {
        Assert.Null(new TotalesExtraidos(null, 52.5m, null, 300m).TotalCalculado);
        Assert.Null(new TotalesExtraidos(250m, null, null, 300m).TotalCalculado);
        Assert.False(new TotalesExtraidos(null, null, null, 300m).CuadreVerificable);
    }

    [Fact]
    public void CamposObligatoriosAusentes_enumera_lo_que_falta()
    {
        var factura = FacturaDePrueba.Valida() with
        {
            Emisor = FacturaDePrueba.Valida().Emisor with { Nif = null },
            Totales = FacturaDePrueba.Valida().Totales with { Total = null },
        };

        Assert.Equal(["emisor.nif", "totales.total"], factura.CamposObligatoriosAusentes());
        Assert.Empty(FacturaDePrueba.Valida().CamposObligatoriosAusentes());
    }
}

public class NifTests
{
    [Theory]
    [InlineData("B-12.345.678", "B12345678")]
    [InlineData("b12345678", "B12345678")]
    [InlineData(" IE 3727924LH ", "IE3727924LH")]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData("---", null)]
    public void Normalizar_limpia_separadores_y_mayusculas(string? entrada, string? esperado)
    {
        Assert.Equal(esperado, Nif.Normalizar(entrada));
    }

    [Theory]
    [InlineData("B12345678", true)]     // CIF
    [InlineData("12345678Z", true)]     // DNI
    [InlineData("X1234567L", true)]     // NIE
    [InlineData("NL859911743B01", true)] // VAT UE
    [InlineData("HOLA", false)]
    [InlineData("123", false)]
    public void FormatoValido_es_y_vat_ue(string nif, bool esperado)
    {
        Assert.Equal(esperado, Nif.FormatoValido(nif));
    }
}
