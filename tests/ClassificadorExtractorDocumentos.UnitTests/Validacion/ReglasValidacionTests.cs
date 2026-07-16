using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Validacion;
using ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;

namespace ClassificadorExtractorDocumentos.UnitTests.Validacion;

using static FacturaDePrueba;

public class ReglaCuadreLineasTests
{
    private readonly ReglaCuadreLineas _regla = new();

    [Fact]
    public void Pasa_cuando_lineas_suman_la_base()
    {
        Assert.Empty(_regla.Validar(Contexto(Valida())));
    }

    [Fact]
    public void Falla_cuando_lineas_no_suman_la_base()
    {
        var factura = Valida() with { Totales = Valida().Totales with { BaseImponible = 300m } };

        var incidencias = _regla.Validar(Contexto(factura)).ToList();

        Assert.Single(incidencias);
        Assert.Equal("CUADRE_LINEAS", incidencias[0].Codigo);
        Assert.Equal(SeveridadIncidencia.Revision, incidencias[0].Severidad);
    }

    [Fact]
    public void Pasa_dentro_de_la_tolerancia_de_2_centimos()
    {
        var factura = Valida() with { Totales = Valida().Totales with { BaseImponible = 250.02m } };

        Assert.Empty(_regla.Validar(Contexto(factura)));
    }

    [Fact]
    public void Con_lineas_iva_incluido_compara_contra_base_mas_cuota()
    {
        var factura = Valida() with
        {
            LineasIncluyenIva = true,
            Lineas = [new LineaExtraida("Producto", 1, 302.5m, 21m, 302.5m)],
        };

        Assert.Empty(_regla.Validar(Contexto(factura)));
    }
}

public class ReglaCuadreTotalTests
{
    private readonly ReglaCuadreTotal _regla = new();

    [Fact]
    public void Pasa_cuando_base_mas_iva_es_el_total()
    {
        Assert.Empty(_regla.Validar(Contexto(Valida())));
    }

    [Fact]
    public void Falla_cuando_el_total_no_cuadra()
    {
        var factura = Valida() with { Totales = Valida().Totales with { Total = 999m } };

        var incidencias = _regla.Validar(Contexto(factura)).ToList();

        Assert.Single(incidencias);
        Assert.Equal(SeveridadIncidencia.Revision, incidencias[0].Severidad);
    }

    [Fact]
    public void Resta_el_irpf_al_cuadrar()
    {
        // 250 + 52.5 − 37.5 = 265 (factura de profesional con retención)
        var factura = Valida() with { Totales = new TotalesExtraidos(250m, 52.5m, 37.5m, 265m) };

        Assert.Empty(_regla.Validar(Contexto(factura)));
    }

    [Fact]
    public void Base_no_impresa_en_documento_pide_revision()
    {
        // Caso plantilla B: solo se imprime el total con IVA incluido
        var factura = Valida() with { Totales = new TotalesExtraidos(null, null, null, 302.5m) };

        var incidencias = _regla.Validar(Contexto(factura)).ToList();

        Assert.Single(incidencias);
        Assert.Equal(SeveridadIncidencia.Revision, incidencias[0].Severidad);
        Assert.Contains("no es verificable", incidencias[0].Detalle);
    }
}

public class ReglaIvaCoherenteTests
{
    private readonly ReglaIvaCoherente _regla = new();

    [Fact]
    public void Pasa_cuando_cuota_coincide_con_lineas()
    {
        Assert.Empty(_regla.Validar(Contexto(Valida())));
    }

    [Fact]
    public void Falla_cuando_cuota_declarada_no_coincide()
    {
        var factura = Valida() with { Totales = Valida().Totales with { CuotaIva = 10m } };

        var incidencias = _regla.Validar(Contexto(factura)).ToList();

        Assert.Single(incidencias);
        Assert.Equal("IVA_COHERENTE", incidencias[0].Codigo);
    }

    [Fact]
    public void Con_tipos_mixtos_por_linea_calcula_cada_tipo()
    {
        // 100×21% + 200×10% = 21 + 20 = 41
        var factura = Valida() with
        {
            Lineas =
            [
                new LineaExtraida("A", 1, 100m, 21m, 100m),
                new LineaExtraida("B", 1, 200m, 10m, 200m),
            ],
            Totales = new TotalesExtraidos(300m, 41m, null, 341m),
        };

        Assert.Empty(_regla.Validar(Contexto(factura)));
    }

    // ── Camino 2: sin %IVA por línea, pero con %IVA global en los totales ──

    [Fact]
    public void Usa_el_tipo_global_cuando_las_lineas_no_traen_iva()
    {
        // Líneas sin %IVA; totales: base 250, IVA 52,5, tipo global 21% → 250×21% = 52,5 ✓
        var factura = Valida() with
        {
            Lineas = [new LineaExtraida("Servicio", 1, 250m, null, 250m)],
            Totales = new TotalesExtraidos(250m, 52.5m, null, 302.5m, PorcentajeIva: 21m),
        };

        Assert.Empty(_regla.Validar(Contexto(factura)));
    }

    [Fact]
    public void Falla_si_el_tipo_global_no_cuadra_con_la_cuota()
    {
        // base 250 × 21% = 52,5, pero declara 40 → incidencia
        var factura = Valida() with
        {
            Lineas = [new LineaExtraida("Servicio", 1, 250m, null, 250m)],
            Totales = new TotalesExtraidos(250m, 40m, null, 290m, PorcentajeIva: 21m),
        };

        var incidencias = _regla.Validar(Contexto(factura)).ToList();

        Assert.Single(incidencias);
        Assert.Contains("tipo global", incidencias[0].Detalle);
    }

    [Fact]
    public void Deriva_la_base_del_total_si_no_hay_base()
    {
        // Solo total 302,5 y tipo global 21% → base derivada 250, cuota esperada 52,5 ✓
        var factura = Valida() with
        {
            Lineas = [new LineaExtraida("Servicio", 1, 250m, null, 250m)],
            Totales = new TotalesExtraidos(null, 52.5m, null, 302.5m, PorcentajeIva: 21m),
        };

        Assert.Empty(_regla.Validar(Contexto(factura)));
    }

    // ── Camino 3: ni %IVA por línea ni global → no verificable ──

    [Fact]
    public void Marca_revision_si_no_puede_verificar_el_iva()
    {
        var factura = Valida() with
        {
            Lineas = [new LineaExtraida("Servicio", 1, 250m, null, 250m)],
            Totales = new TotalesExtraidos(250m, 52.5m, null, 302.5m), // sin %IVA global
        };

        var incidencias = _regla.Validar(Contexto(factura)).ToList();

        Assert.Single(incidencias);
        Assert.Equal(SeveridadIncidencia.Revision, incidencias[0].Severidad);
        Assert.Contains("No se puede verificar", incidencias[0].Detalle);
    }

    [Fact]
    public void Reverse_charge_no_dispara_no_verificable()
    {
        // Sin %IVA por línea ni global, pero reverse charge → no debe pedir revisión por IVA
        var factura = ReverseCharge() with
        {
            Lineas = [new LineaExtraida("Cloud", 1, 250m, null, 250m)],
            Totales = new TotalesExtraidos(250m, 0m, null, 250m),
        };

        Assert.Empty(_regla.Validar(Contexto(factura)));
    }
}

public class ReglaReverseChargeTests
{
    private readonly ReglaReverseCharge _regla = new();

    [Fact]
    public void Reverse_charge_con_iva_cero_no_genera_incidencia()
    {
        Assert.Empty(_regla.Validar(Contexto(ReverseCharge())));
    }

    [Fact]
    public void Reverse_charge_con_iva_distinto_de_cero_genera_info()
    {
        var factura = ReverseCharge() with { Totales = new TotalesExtraidos(250m, 52.5m, null, 302.5m) };

        var incidencias = _regla.Validar(Contexto(factura)).ToList();

        Assert.Single(incidencias);
        Assert.Equal(SeveridadIncidencia.Info, incidencias[0].Severidad);
    }
}

public class ReglaNifFormatoTests
{
    private readonly ReglaNifFormato _regla = new();

    [Theory]
    [InlineData("B12345678")]    // CIF
    [InlineData("12345678Z")]    // DNI
    [InlineData("X1234567L")]    // NIE
    [InlineData("IE3727924LH")]  // VAT Irlanda
    [InlineData("NL859911743B01")] // VAT Países Bajos
    public void Pasa_con_formatos_validos(string nif)
    {
        var factura = Valida() with { Emisor = Valida().Emisor with { Nif = nif } };

        Assert.Empty(_regla.Validar(Contexto(factura)));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("HOLA")]
    [InlineData("B123")]
    public void Falla_con_formatos_invalidos(string nif)
    {
        var factura = Valida() with { Emisor = Valida().Emisor with { Nif = nif } };

        var incidencias = _regla.Validar(Contexto(factura)).ToList();

        Assert.Single(incidencias);
        Assert.Equal(SeveridadIncidencia.Revision, incidencias[0].Severidad);
    }

    [Fact]
    public void Nif_ausente_no_es_asunto_de_esta_regla()
    {
        var factura = Valida() with
        {
            Emisor = Valida().Emisor with { Nif = null },
            Receptor = Valida().Receptor with { Nif = null },
        };

        Assert.Empty(_regla.Validar(Contexto(factura)));
    }
}

public class ReglaCamposObligatoriosTests
{
    private readonly ReglaCamposObligatorios _regla = new();

    [Fact]
    public void Pasa_con_todos_los_obligatorios_presentes()
    {
        Assert.Empty(_regla.Validar(Contexto(Valida())));
    }

    [Fact]
    public void Falla_con_rechazo_si_falta_algun_obligatorio()
    {
        var factura = Valida() with
        {
            Emisor = Valida().Emisor with { Nif = null },
            Totales = Valida().Totales with { Total = null },
        };

        var incidencias = _regla.Validar(Contexto(factura)).ToList();

        Assert.Single(incidencias);
        Assert.Equal(SeveridadIncidencia.Rechazo, incidencias[0].Severidad);
        Assert.Contains("emisor.nif", incidencias[0].Detalle);
        Assert.Contains("totales.total", incidencias[0].Detalle);
    }
}

public class ReglaConfidenceMinimaTests
{
    private readonly ReglaConfidenceMinima _regla = new();

    [Fact]
    public void Pasa_con_confianzas_altas()
    {
        Assert.Empty(_regla.Validar(Contexto(Valida())));
    }

    [Fact]
    public void Falla_si_un_obligatorio_presente_tiene_confianza_baja()
    {
        var factura = Valida();
        factura = factura with
        {
            Metadatos = factura.Metadatos with
            {
                ConfidencePorCampo = new Dictionary<string, double>(factura.Metadatos.ConfidencePorCampo)
                {
                    ["factura.numero"] = 0.5,
                },
            },
        };

        var incidencias = _regla.Validar(Contexto(factura)).ToList();

        Assert.Single(incidencias);
        Assert.Contains("factura.numero", incidencias[0].Detalle);
    }

    [Fact]
    public void Campo_ausente_con_confidence_cero_no_duplica_incidencia()
    {
        // El ausente ya es Rechazo por CAMPOS_OBLIGATORIOS; esta regla no debe sumar otra
        var factura = Valida() with { Emisor = Valida().Emisor with { Nif = null } };
        factura = factura with
        {
            Metadatos = factura.Metadatos with
            {
                ConfidencePorCampo = new Dictionary<string, double>(factura.Metadatos.ConfidencePorCampo)
                {
                    ["emisor.nif"] = 0.0,
                },
            },
        };

        Assert.Empty(_regla.Validar(Contexto(factura)));
    }
}

public class ReglaFechaRazonableTests
{
    private readonly ReglaFechaRazonable _regla = new();

    [Fact]
    public void Pasa_con_fecha_reciente()
    {
        Assert.Empty(_regla.Validar(Contexto(Valida())));
    }

    [Fact]
    public void Falla_con_fecha_futura()
    {
        var factura = Valida() with { Factura = Valida().Factura with { Fecha = "2027-01-01" } };

        var incidencias = _regla.Validar(Contexto(factura)).ToList();

        Assert.Single(incidencias);
        Assert.Contains("futura", incidencias[0].Detalle);
    }

    [Fact]
    public void Falla_con_fecha_anterior_a_10_anios()
    {
        var factura = Valida() with { Factura = Valida().Factura with { Fecha = "2015-01-01" } };

        var incidencias = _regla.Validar(Contexto(factura)).ToList();

        Assert.Single(incidencias);
        Assert.Contains("10 años", incidencias[0].Detalle);
    }
}

public class ReglaDuplicadoTests
{
    private readonly ReglaDuplicado _regla = new();

    [Fact]
    public void Pasa_si_no_hay_duplicado()
    {
        Assert.Empty(_regla.Validar(Contexto(Valida(), duplicado: false)));
    }

    [Fact]
    public void Falla_con_rechazo_si_ya_existe()
    {
        var incidencias = _regla.Validar(Contexto(Valida(), duplicado: true)).ToList();

        Assert.Single(incidencias);
        Assert.Equal("DUPLICADO", incidencias[0].Codigo);
        Assert.Equal(SeveridadIncidencia.Rechazo, incidencias[0].Severidad);
    }
}
