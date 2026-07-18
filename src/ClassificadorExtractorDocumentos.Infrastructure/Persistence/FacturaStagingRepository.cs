using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence;

public class FacturaStagingRepository(DocFlowDbContext db, TimeProvider timeProvider) : IFacturaStagingRepository
{
    public Task<bool> ExisteFacturaAsync(string nifEmisor, string numeroFactura, CancellationToken cancellationToken = default) =>
        db.FacturasStaging.AnyAsync(
            f => f.NumeroFactura == numeroFactura && f.Proveedor != null && f.Proveedor.Nif == nifEmisor
                && f.FechaEliminacion == null,
            cancellationToken);

    public async Task<int> GuardarAsync(
        FacturaStaging factura,
        string? nifProveedor,
        string? nombreProveedor,
        CancellationToken cancellationToken = default)
    {
        // Transacción explícita: alta de proveedor + factura + líneas + incidencias, todo o nada
        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);

        if (nifProveedor is not null)
        {
            var proveedor = await db.Proveedores.FirstOrDefaultAsync(p => p.Nif == nifProveedor, cancellationToken);
            if (proveedor is null)
            {
                proveedor = new Proveedor
                {
                    Nif = nifProveedor,
                    Nombre = nombreProveedor ?? nifProveedor,
                    FechaAlta = DateTime.UtcNow,
                };
                db.Proveedores.Add(proveedor);
            }
            factura.Proveedor = proveedor;
        }

        db.FacturasStaging.Add(factura); // líneas e incidencias van en cascada por navegación

        await db.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        return factura.Id;
    }

    public async Task<bool> EliminarAsync(int facturaId, CancellationToken cancellationToken = default)
    {
        var factura = await db.FacturasStaging.FirstOrDefaultAsync(f => f.Id == facturaId, cancellationToken);
        if (factura is null)
        {
            return false;
        }

        factura.FechaEliminacion ??= timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ResultadoAprobacion> AprobarAsync(int facturaId, CancellationToken cancellationToken = default)
    {
        var factura = await db.FacturasStaging.FirstOrDefaultAsync(f => f.Id == facturaId, cancellationToken);
        if (factura is null)
        {
            return ResultadoAprobacion.Fallo($"No existe ninguna factura con id {facturaId}.");
        }
        if (factura.FechaEliminacion is not null)
        {
            return ResultadoAprobacion.Fallo("La factura está eliminada.");
        }
        if (factura.Estado != EstadoFactura.RevisionHumana)
        {
            return ResultadoAprobacion.Fallo(
                $"Solo se puede aprobar manualmente una factura en RevisionHumana (estado actual: {factura.Estado}).");
        }

        factura.Estado = EstadoFactura.Validada;
        factura.FechaAprobacionManual = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return ResultadoAprobacion.Ok();
    }
}
