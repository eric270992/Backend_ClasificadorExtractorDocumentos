using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Validacion;

namespace ClassificadorExtractorDocumentos.UnitTests.Validacion;

/// <summary>Builder de facturas válidas para tests: cada test muta solo lo que necesita romper.</summary>
public static class FacturaDePrueba
{
    public static readonly DateOnly Hoy = new(2026, 7, 12);

    /// <summary>Factura coherente: 2 líneas sin IVA incluido, 21%, cuadres exactos.</summary>
    public static FacturaExtraida Valida() => new(
        Emisor: new EmisorExtraido("B12345678", "Proveedor SL", "C/ Mayor 1"),
        Receptor: new ReceptorExtraido("A87654321", "Cliente SA"),
        Factura: new DatosFacturaExtraidos("F-2026-001", "2026-07-01", null, "EUR"),
        Lineas:
        [
            new LineaExtraida("Producto A", 2, 100m, 21m, 200m),
            new LineaExtraida("Producto B", 1, 50m, 21m, 50m),
        ],
        LineasIncluyenIva: false,
        Totales: new TotalesExtraidos(BaseImponible: 250m, CuotaIva: 52.5m, RetencionIrpf: null, Total: 302.5m),
        Metadatos: new MetadatosExtraidos("es", ReverseCharge: false, new Dictionary<string, double>
        {
            ["emisor.nif"] = 0.98,
            ["factura.numero"] = 0.97,
            ["factura.fecha"] = 0.99,
            ["totales.total"] = 0.99,
        }));

    /// <summary>Factura intracomunitaria con inversión del sujeto pasivo (estilo TechCloud): IVA 0 legítimo.</summary>
    public static FacturaExtraida ReverseCharge() => Valida() with
    {
        Emisor = new EmisorExtraido("IE1234567X", "TechCloud Ltd", null),
        Lineas = [new LineaExtraida("Cloud services", 1, 250m, 0m, 250m)],
        Totales = new TotalesExtraidos(250m, 0m, null, 250m),
        Metadatos = Valida().Metadatos with { ReverseCharge = true },
    };

    public static ContextoValidacion Contexto(FacturaExtraida factura, bool duplicado = false) =>
        new(factura, duplicado, Hoy);
}
