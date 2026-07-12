using ClassificadorExtractorDocumentos.Application.Validacion;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Entities;
using ClassificadorExtractorDocumentos.Domain.Validacion;
using ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;

namespace ClassificadorExtractorDocumentos.UnitTests.Validacion;

using static FacturaDePrueba;

public class ValidadorAgentTests
{
    private static readonly IReglaValidacion[] TodasLasReglas =
    [
        new ReglaCuadreLineas(),
        new ReglaCuadreTotal(),
        new ReglaIvaCoherente(),
        new ReglaReverseCharge(),
        new ReglaNifFormato(),
        new ReglaCamposObligatorios(),
        new ReglaConfidenceMinima(),
        new ReglaFechaRazonable(),
        new ReglaDuplicado(),
    ];

    private readonly ValidadorAgent _validador = new(TodasLasReglas);

    [Fact]
    public void Factura_coherente_queda_validada_sin_incidencias()
    {
        var resultado = _validador.Validar(Contexto(Valida()));

        Assert.Equal(EstadoFactura.Validada, resultado.Estado);
        Assert.Empty(resultado.Incidencias);
    }

    [Fact]
    public void Factura_reverse_charge_queda_validada_sin_incidencia_de_iva()
    {
        // Criterio de aceptación E1-F3: la factura reverse charge NO genera incidencia de IVA
        var resultado = _validador.Validar(Contexto(ReverseCharge()));

        Assert.Equal(EstadoFactura.Validada, resultado.Estado);
        Assert.Empty(resultado.Incidencias);
    }

    [Fact]
    public void Una_revision_deriva_a_revision_humana()
    {
        var factura = Valida() with { Totales = Valida().Totales with { Total = 999m } };

        var resultado = _validador.Validar(Contexto(factura));

        Assert.Equal(EstadoFactura.RevisionHumana, resultado.Estado);
        Assert.Contains(resultado.Incidencias, i => i.Codigo == "CUADRE_TOTAL");
    }

    [Fact]
    public void Un_rechazo_gana_sobre_las_revisiones()
    {
        var resultado = _validador.Validar(Contexto(Valida(), duplicado: true));

        Assert.Equal(EstadoFactura.Rechazada, resultado.Estado);
    }

    [Fact]
    public void Incidencia_info_no_penaliza_el_estado()
    {
        // Reverse charge declarado con cuota != 0: Info (y CUADRE/IVA sí darán Revisión aparte).
        // Aquí solo Info: cuota coherente con líneas al 0% pero declarada reverse charge... construimos
        // el caso mínimo: sin líneas y total = base para no despertar otras reglas.
        var factura = ReverseCharge() with
        {
            Lineas = [],
            Totales = new TotalesExtraidos(250m, 0.01m, null, 250.01m),
        };

        var resultado = _validador.Validar(Contexto(factura));

        Assert.Contains(resultado.Incidencias, i => i.Severidad == SeveridadIncidencia.Info);
        Assert.Equal(EstadoFactura.Validada, resultado.Estado);
    }

    [Fact]
    public void Albaran_sin_datos_de_factura_queda_rechazado()
    {
        // Caso real BRUMAT: documento sin número, fecha, ni totales
        var factura = Valida() with
        {
            Emisor = new EmisorExtraido(null, "BRUMAT", null),
            Factura = new DatosFacturaExtraidos(null, null, null, "EUR"),
            Lineas = [],
            Totales = new TotalesExtraidos(null, null, null, null),
        };

        var resultado = _validador.Validar(Contexto(factura));

        Assert.Equal(EstadoFactura.Rechazada, resultado.Estado);
        Assert.Contains(resultado.Incidencias, i => i.Codigo == "CAMPOS_OBLIGATORIOS");
    }
}
