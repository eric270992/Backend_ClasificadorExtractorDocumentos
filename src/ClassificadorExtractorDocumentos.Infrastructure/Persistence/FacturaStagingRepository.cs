using System.Globalization;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence;

public class FacturaStagingRepository(DocFlowDbContext db, TimeProvider timeProvider) : IFacturaStagingRepository
{
    /// <summary>Sin autenticación todavía en el sistema: mientras no haya login, todo cambio manual
    /// se atribuye a este usuario fijo en el historial de auditoría.</summary>
    private const string UsuarioPorDefecto = "Usuario desconocido";

    public Task<bool> ExisteFacturaAsync(string nifEmisor, string numeroFactura, CancellationToken cancellationToken = default) =>
        db.FacturasStaging.AnyAsync(
            f => f.NumeroFactura == numeroFactura && f.Proveedor != null && f.Proveedor.Nif == nifEmisor
                && f.FechaEliminacion == null && f.Estado != EstadoFactura.Rechazada,
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

    public async Task<ResultadoAprobacion> ActualizarAsync(
        int facturaId,
        ActualizarFacturaRequest request,
        CancellationToken cancellationToken = default)
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
        if (factura.Estado is not (EstadoFactura.Validada or EstadoFactura.RevisionHumana))
        {
            return ResultadoAprobacion.Fallo(
                $"Solo se puede editar una factura en Validada o RevisionHumana (estado actual: {factura.Estado}).");
        }

        var ahora = timeProvider.GetUtcNow().UtcDateTime;
        var cambios = new List<HistorialCambioFactura>();

        void RegistrarSiCambia(string campo, string? valorAnterior, string? valorNuevo)
        {
            if (valorAnterior == valorNuevo)
            {
                return;
            }
            cambios.Add(new HistorialCambioFactura
            {
                FacturaId = facturaId,
                Campo = campo,
                ValorAnterior = valorAnterior,
                ValorNuevo = valorNuevo,
                FechaCambio = ahora,
                Usuario = UsuarioPorDefecto,
            });
        }

        RegistrarSiCambia(nameof(FacturaStaging.NifCliente), factura.NifCliente, request.NifCliente);
        RegistrarSiCambia(nameof(FacturaStaging.NombreCliente), factura.NombreCliente, request.NombreCliente);
        RegistrarSiCambia(nameof(FacturaStaging.NumeroFactura), factura.NumeroFactura, request.NumeroFactura);
        RegistrarSiCambia(nameof(FacturaStaging.FechaFactura), Texto(factura.FechaFactura), Texto(request.FechaFactura));
        RegistrarSiCambia(nameof(FacturaStaging.FechaVencimiento), Texto(factura.FechaVencimiento), Texto(request.FechaVencimiento));
        RegistrarSiCambia(nameof(FacturaStaging.Moneda), factura.Moneda, request.Moneda);
        RegistrarSiCambia(nameof(FacturaStaging.BaseImponible), Texto(factura.BaseImponible), Texto(request.BaseImponible));
        RegistrarSiCambia(nameof(FacturaStaging.CuotaIva), Texto(factura.CuotaIva), Texto(request.CuotaIva));
        RegistrarSiCambia(nameof(FacturaStaging.RetencionIrpf), Texto(factura.RetencionIrpf), Texto(request.RetencionIrpf));
        RegistrarSiCambia(nameof(FacturaStaging.Total), Texto(factura.Total), Texto(request.Total));
        RegistrarSiCambia(nameof(FacturaStaging.ReverseCharge), Texto(factura.ReverseCharge), Texto(request.ReverseCharge));

        if (cambios.Count == 0)
        {
            return ResultadoAprobacion.Ok();
        }

        factura.NifCliente = request.NifCliente;
        factura.NombreCliente = request.NombreCliente;
        factura.NumeroFactura = request.NumeroFactura;
        factura.FechaFactura = request.FechaFactura;
        factura.FechaVencimiento = request.FechaVencimiento;
        factura.Moneda = request.Moneda;
        factura.BaseImponible = request.BaseImponible;
        factura.CuotaIva = request.CuotaIva;
        factura.RetencionIrpf = request.RetencionIrpf;
        factura.Total = request.Total;
        factura.ReverseCharge = request.ReverseCharge;

        db.HistorialCambiosFactura.AddRange(cambios);
        await db.SaveChangesAsync(cancellationToken);
        return ResultadoAprobacion.Ok();
    }

    private static string? Texto(DateOnly? valor) => valor?.ToString("O");
    private static string Texto(DateOnly valor) => valor.ToString("O");
    private static string? Texto(decimal? valor) => valor?.ToString(CultureInfo.InvariantCulture);
    private static string Texto(decimal valor) => valor.ToString(CultureInfo.InvariantCulture);
    private static string Texto(bool valor) => valor.ToString(CultureInfo.InvariantCulture);
}
