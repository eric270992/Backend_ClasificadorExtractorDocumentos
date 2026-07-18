using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence;

/// <summary>
/// Implementación de <see cref="IFacturaConsultaService"/> con EF Core. Es el adaptador de lectura:
/// proyecta las entidades a DTOs directamente en la consulta SQL (no salen entidades EF de aquí).
/// </summary>
public class FacturaConsultaService(DocFlowDbContext db) : IFacturaConsultaService
{
    public async Task<IReadOnlyList<FacturaResumen>> ListarAsync(CancellationToken cancellationToken = default)
    {
        return await db.FacturasStaging
            .AsNoTracking()
            .Where(f => f.FechaEliminacion == null)
            .OrderByDescending(f => f.FechaIngesta)
            .Select(f => new FacturaResumen(
                f.Id,
                f.Proveedor != null ? f.Proveedor.Nombre : null,
                f.Proveedor != null ? f.Proveedor.Nif : null,
                f.NumeroFactura,
                f.FechaFactura,
                f.Moneda,
                f.Total,
                f.Estado.ToString(),
                f.Incidencias.Count,
                f.FechaIngesta))
            .ToListAsync(cancellationToken);
    }

    public async Task<FacturaDetalle?> ObtenerDetalleAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db.FacturasStaging
            .AsNoTracking()
            .Where(f => f.Id == id && f.FechaEliminacion == null)
            .Select(f => new FacturaDetalle(
                f.Id,
                f.Proveedor != null ? new ProveedorResumen(f.Proveedor.Nif, f.Proveedor.Nombre) : null,
                f.NumeroFactura,
                f.FechaFactura,
                f.FechaVencimiento,
                f.Moneda,
                f.BaseImponible,
                f.CuotaIva,
                f.RetencionIrpf,
                f.Total,
                f.ReverseCharge,
                f.Estado.ToString(),
                f.NivelExtraccion,
                f.FechaIngesta,
                f.FechaAprobacionManual,
                f.Lineas
                    .OrderBy(l => l.NumLinea)
                    .Select(l => new LineaResumen(
                        l.NumLinea, l.Descripcion, l.Cantidad, l.PrecioUnitario, l.PorcentajeIva, l.ImporteLinea))
                    .ToList(),
                f.Incidencias
                    .Select(i => new IncidenciaResumen(i.Codigo, i.Detalle, i.FechaCreacion))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
