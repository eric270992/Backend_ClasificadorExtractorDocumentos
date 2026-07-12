using ClassificadorExtractorDocumentos.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassificadorExtractorDocumentos.Api.Controllers;

/// <summary>Consulta de estado e incidencias del staging (SPEC E1-F3, T3.3).</summary>
[ApiController]
[Route("facturas")]
public class FacturasController(DocFlowDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var facturas = await db.FacturasStaging
            .AsNoTracking()
            .Include(f => f.Proveedor)
            .OrderByDescending(f => f.FechaIngesta)
            .Select(f => new
            {
                f.Id,
                Proveedor = f.Proveedor != null ? f.Proveedor.Nombre : null,
                NifProveedor = f.Proveedor != null ? f.Proveedor.Nif : null,
                f.NumeroFactura,
                f.FechaFactura,
                f.Moneda,
                f.Total,
                Estado = f.Estado.ToString(),
                NumIncidencias = f.Incidencias.Count,
                f.FechaIngesta,
            })
            .ToListAsync(cancellationToken);

        return Ok(facturas);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancellationToken)
    {
        var factura = await db.FacturasStaging
            .AsNoTracking()
            .Include(f => f.Proveedor)
            .Include(f => f.Lineas.OrderBy(l => l.NumLinea))
            .Include(f => f.Incidencias)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (factura is null)
        {
            return NotFound($"No existe ninguna factura con id {id}.");
        }

        return Ok(new
        {
            factura.Id,
            Proveedor = factura.Proveedor is null ? null : new { factura.Proveedor.Nif, factura.Proveedor.Nombre },
            factura.NumeroFactura,
            factura.FechaFactura,
            factura.FechaVencimiento,
            factura.Moneda,
            factura.BaseImponible,
            factura.CuotaIva,
            factura.RetencionIrpf,
            factura.Total,
            factura.ReverseCharge,
            Estado = factura.Estado.ToString(),
            factura.NivelExtraccion,
            factura.FechaIngesta,
            Lineas = factura.Lineas.Select(l => new
            {
                l.NumLinea, l.Descripcion, l.Cantidad, l.PrecioUnitario, l.PorcentajeIva, l.ImporteLinea,
            }),
            Incidencias = factura.Incidencias.Select(i => new { i.Codigo, i.Detalle, i.FechaCreacion }),
        });
    }
}
