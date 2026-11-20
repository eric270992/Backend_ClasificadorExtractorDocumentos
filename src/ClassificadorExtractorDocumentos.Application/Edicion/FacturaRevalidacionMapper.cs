using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Entities;

namespace ClassificadorExtractorDocumentos.Application.Edicion;

/// <summary>
/// Converteix una factura de staging (ja editada) en <see cref="FacturaExtraida"/>, que és el que validen les
/// regles. És el camí invers de FacturaStagingMapper.
///
/// La taula de staging no guarda tot el que va dir el model (confiança per camp, si les línies porten l'IVA
/// inclòs, el % d'IVA global imprès, l'idioma). Això es recupera del JSON original de l'extracció.
/// </summary>
public static class FacturaRevalidacionMapper
{
    /// <summary>Confiança d'un camp que ha revisat una persona: ja no és una suposició del model.</summary>
    public const double ConfianzaRevisadaPorHumano = 1.0;

    /// <summary>Camp de l'historial → clau de confiança del model (les de FacturaExtraida.CamposObligatorios i altres).</summary>
    private static readonly Dictionary<string, string> ClavesConfianza = new()
    {
        [nameof(FacturaStaging.NumeroFactura)] = "factura.numero",
        [nameof(FacturaStaging.FechaFactura)] = "factura.fecha",
        [nameof(FacturaStaging.Total)] = "totales.total",
        [nameof(FacturaStaging.BaseImponible)] = "totales.baseImponible",
        [nameof(FacturaStaging.CuotaIva)] = "totales.cuotaIva",
    };

    /// <param name="camposEditados">Camps editats a mà alguna vegada (historial + aquesta edició).</param>
    public static FacturaExtraida Map(FacturaStaging factura, IReadOnlySet<string> camposEditados)
    {
        // Si el JSON original no es pogués llegir (no hauria de passar: ja es va llegir en la ingesta),
        // es continua amb valors neutres: sense confiances, la regla CONFIDENCE_MINIMA no diu res.
        var original = FacturaExtraidaParser.Parse(factura.JsonOriginalExtraido).Factura;

        var confianza = new Dictionary<string, double>(original?.Metadatos.ConfidencePorCampo ?? new Dictionary<string, double>());
        foreach (var campo in camposEditados)
        {
            if (ClavesConfianza.TryGetValue(campo, out var clave))
            {
                confianza[clave] = ConfianzaRevisadaPorHumano;
            }
            else if (campo.StartsWith("Línea", StringComparison.Ordinal))
            {
                confianza["lineas"] = ConfianzaRevisadaPorHumano;
            }
        }

        // El % d'IVA global imprès al document només té sentit mentre base i quota siguin les llegides.
        // Si l'usuari n'ha canviat alguna, el % es deixa a null i les regles el dedueixen de les xifres noves.
        var totalesEditados = camposEditados.Contains(nameof(FacturaStaging.BaseImponible)) ||
                              camposEditados.Contains(nameof(FacturaStaging.CuotaIva));
        var porcentajeIvaGlobal = totalesEditados ? null : original?.Totales.PorcentajeIva;

        return new FacturaExtraida(
            Emisor: new EmisorExtraido(factura.Proveedor?.Nif, factura.Proveedor?.Nombre, original?.Emisor.Direccion),
            Receptor: new ReceptorExtraido(factura.NifCliente, factura.NombreCliente),
            Factura: new DatosFacturaExtraidos(
                // Un número buit equival a "no hi és": així la regla de camps obligatoris el detecta
                Numero: string.IsNullOrWhiteSpace(factura.NumeroFactura) ? null : factura.NumeroFactura.Trim(),
                Fecha: factura.FechaFactura.ToString("yyyy-MM-dd"),
                Vencimiento: factura.FechaVencimiento?.ToString("yyyy-MM-dd"),
                Moneda: factura.Moneda),
            Lineas: factura.Lineas
                .OrderBy(l => l.NumLinea)
                .Select(l => new LineaExtraida(l.Descripcion, l.Cantidad, l.PrecioUnitario, l.PorcentajeIva, l.ImporteLinea))
                .ToList(),
            // ContextoValidacion.Crear el corregeix si amb les xifres noves l'altre valor quadra millor
            LineasIncluyenIva: original?.LineasIncluyenIva ?? false,
            Totales: new TotalesExtraidos(
                factura.BaseImponible, factura.CuotaIva, factura.RetencionIrpf, factura.Total, porcentajeIvaGlobal),
            Metadatos: new MetadatosExtraidos(original?.Metadatos.Idioma, factura.ReverseCharge, confianza));
    }
}
