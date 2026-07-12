using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence;

public class FacturaStagingRepository(DocFlowDbContext db) : IFacturaStagingRepository
{
    public Task<bool> ExisteFacturaAsync(string nifEmisor, string numeroFactura, CancellationToken cancellationToken = default) =>
        db.FacturasStaging.AnyAsync(
            f => f.NumeroFactura == numeroFactura && f.Proveedor != null && f.Proveedor.Nif == nifEmisor,
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
}
