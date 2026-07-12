using System.Globalization;
using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Application.Validacion;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Entities;

namespace ClassificadorExtractorDocumentos.Application.Ingesta;

/// <summary>
/// Mapea el resultado de extracción + validación a la entidad de staging. Compartido por ambos
/// pipelines (manual y MAF). Los campos NOT NULL reciben placeholders documentados cuando la
/// extracción devolvió null; la verdad del documento se conserva en JsonOriginalExtraido.
/// </summary>
public static class FacturaStagingMapper
{
    public static FacturaStaging Map(
        DocumentoIngestado documento,
        ResultadoExtraccion extraccion,
        ResultadoValidacion validacion,
        DateTime fechaIngesta)
    {
        var f = extraccion.Factura!;

        return new FacturaStaging
        {
            NumeroFactura = f.Factura.Numero ?? $"(SIN-NUMERO)-{documento.Id.ToString()[..8]}",
            FechaFactura = ParsearFecha(f.Factura.Fecha) ?? DateOnly.FromDateTime(fechaIngesta),
            FechaVencimiento = ParsearFecha(f.Factura.Vencimiento),
            Moneda = f.Factura.Moneda.Length == 3 ? f.Factura.Moneda : "EUR",
            BaseImponible = f.Totales.BaseImponible ?? 0m,
            CuotaIva = f.Totales.CuotaIva ?? 0m,
            RetencionIrpf = f.Totales.RetencionIrpf,
            Total = f.Totales.Total ?? 0m,
            ReverseCharge = f.Metadatos.ReverseCharge,
            Estado = validacion.Estado,
            JsonOriginalExtraido = extraccion.JsonOriginal!,
            NivelExtraccion = ExtractorAgent.NivelExtraccionGenerica,
            RutaPdfOriginal = documento.RutaPdf,
            FechaIngesta = fechaIngesta,
            Lineas = f.Lineas.Select((l, i) => new FacturaLinea
            {
                NumLinea = i + 1,
                Descripcion = l.Descripcion ?? "(sin descripción)",
                Cantidad = l.Cantidad ?? 0m,
                PrecioUnitario = l.PrecioUnitario ?? 0m,
                PorcentajeIva = l.PorcentajeIva ?? 0m,
                ImporteLinea = l.ImporteLinea ?? 0m,
            }).ToList(),
            Incidencias = validacion.Incidencias.Select(i => new ValidacionIncidencia
            {
                Codigo = i.Codigo,
                Detalle = i.Detalle,
                FechaCreacion = fechaIngesta,
            }).ToList(),
        };
    }

    private static DateOnly? ParsearFecha(string? iso) =>
        DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha
            : null;
}
