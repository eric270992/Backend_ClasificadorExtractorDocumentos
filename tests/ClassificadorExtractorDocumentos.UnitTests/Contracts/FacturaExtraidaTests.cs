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
    public void PorcentajeIva_en_fraccion_0a1_se_normaliza_como_si_fuera_porcentaje()
    {
        // El modelo a veces confunde 21 (esperado) con 0.21 (fracción). 100€ al "0.21" debe dar
        // la misma cuota que al 21 (21€): ningún tipo de IVA real está entre 0 y 1.
        var conFraccion = new LineaExtraida("X", 1, 100m, 0.21m, 100m);
        var conEntero = new LineaExtraida("X", 1, 100m, 21m, 100m);

        Assert.Equal(conEntero.CuotaIva(importeIncluyeIva: false), conFraccion.CuotaIva(importeIncluyeIva: false));
        Assert.Equal(21m, conFraccion.CuotaIva(importeIncluyeIva: false));
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
    public void BaseImponibleEfectiva_usa_la_del_documento_si_existe()
    {
        var factura = FacturaDePrueba.Valida(); // Totales con base 250 impresa
        Assert.Equal(250m, factura.BaseImponibleEfectiva());
    }

    [Fact]
    public void BaseYCuotaEfectivas_se_deducen_de_lineas_cuando_el_documento_no_las_trae()
    {
        // Plantilla B: líneas con IVA incluido al 21%, el documento no imprime base/cuota.
        // 121 con IVA incluido → base 100, cuota 21
        var factura = FacturaDePrueba.Valida() with
        {
            LineasIncluyenIva = true,
            Lineas = [new LineaExtraida("A", 1, 121m, 21m, 121m)],
            Totales = new TotalesExtraidos(null, null, null, 121m),
        };

        Assert.Equal(100m, Math.Round(factura.BaseImponibleEfectiva()!.Value, 2));
        Assert.Equal(21m, Math.Round(factura.CuotaIvaEfectiva()!.Value, 2));
    }

    [Fact]
    public void BaseYCuotaEfectivas_son_null_sin_totales_ni_lineas()
    {
        var factura = FacturaDePrueba.Valida() with
        {
            Lineas = [],
            Totales = new TotalesExtraidos(null, null, null, 100m),
        };

        Assert.Null(factura.BaseImponibleEfectiva());
        Assert.Null(factura.CuotaIvaEfectiva());
    }

    [Fact]
    public void LineasIncluyenIvaEfectivo_se_autocorrige_si_lo_declarado_no_es_consistente()
    {
        // Bug real visto con varios proveedores LLM (Nvidia, local): el modelo declara
        // LineasIncluyenIva=true cuando en realidad las líneas son solo base (sin IVA incluido).
        // Lo declarado no cuadra (Σlíneas=214,90 vs base+IVA=260,03); lo contrario sí cuadra en
        // ambos frentes (Σlíneas y cuota por líneas).
        var factura = FacturaDePrueba.Valida() with
        {
            LineasIncluyenIva = true, // declarado por el modelo (incorrecto)
            Lineas =
            [
                new LineaExtraida("A", 2, 4.7m, 21m, 9.4m),
                new LineaExtraida("B", 5, 41.1m, 21m, 205.5m),
            ],
            Totales = new TotalesExtraidos(214.9m, 45.13m, null, 260.03m),
        };

        Assert.False(factura.LineasIncluyenIvaEfectivo(0.02m));
    }

    [Fact]
    public void LineasIncluyenIvaEfectivo_no_cambia_si_lo_declarado_ya_es_consistente()
    {
        Assert.False(FacturaDePrueba.Valida().LineasIncluyenIvaEfectivo(0.02m));
    }

    [Fact]
    public void LineasIncluyenIvaEfectivo_no_cambia_si_ninguna_interpretacion_cuadra()
    {
        // Ni con IVA incluido ni sin él las líneas cuadran con los totales: no hay señal
        // suficiente para autocorregir, se respeta lo declarado (aquí, true).
        var factura = FacturaDePrueba.Valida() with
        {
            LineasIncluyenIva = true,
            Lineas = [new LineaExtraida("A", 1, 999m, 21m, 999m)],
            Totales = new TotalesExtraidos(250m, 52.5m, null, 302.5m),
        };

        Assert.True(factura.LineasIncluyenIvaEfectivo(0.02m));
    }

    [Fact]
    public void CuotaIvaEsperadaPorTipoGlobal_usa_la_base_si_existe()
    {
        // base 250 × 21% = 52,5
        var factura = FacturaDePrueba.Valida() with
        {
            Totales = new TotalesExtraidos(250m, 52.5m, null, 302.5m, PorcentajeIva: 21m),
        };

        Assert.Equal(52.5m, factura.CuotaIvaEsperadaPorTipoGlobal());
    }

    [Fact]
    public void CuotaIvaEsperadaPorTipoGlobal_deriva_del_total_si_no_hay_base()
    {
        // total 302,5 al 21% → cuota 52,5 (base derivada 250)
        var factura = FacturaDePrueba.Valida() with
        {
            Totales = new TotalesExtraidos(null, 52.5m, null, 302.5m, PorcentajeIva: 21m),
        };

        Assert.Equal(52.5m, Math.Round(factura.CuotaIvaEsperadaPorTipoGlobal()!.Value, 2));
    }

    [Fact]
    public void CuotaIvaEsperadaPorTipoGlobal_es_null_sin_tipo_global()
    {
        var factura = FacturaDePrueba.Valida() with
        {
            Totales = new TotalesExtraidos(250m, 52.5m, null, 302.5m),
        };

        Assert.Null(factura.CuotaIvaEsperadaPorTipoGlobal());
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
