using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence;

public class FacturaStagingRepository(DocFlowDbContext db, TimeProvider timeProvider) : IFacturaStagingRepository
{
    /// <summary>Sin autenticación todavía en el sistema: mientras no haya login, todo cambio manual
    /// se atribuye a este usuario fijo en el historial de auditoría.</summary>
    private const string UsuarioPorDefecto = "Usuario desconocido";

    public async Task<bool> ExisteFacturaAsync(string nifEmisor, string numeroFactura, CancellationToken cancellationToken = default)
    {
        // Abans era una expressió d'una línia; ara és async per poder obrir el span al voltant de la consulta
        using var activity = PersistenciaTracing.Source.StartActivity("bd.existe_factura");
        var existe = await db.FacturasStaging.AnyAsync(
            f => f.NumeroFactura == numeroFactura && f.Proveedor != null && f.Proveedor.Nif == nifEmisor
                && f.FechaEliminacion == null && f.Estado != EstadoFactura.Rechazada,
            cancellationToken);
        activity?.SetTag("bd.existe", existe);
        return existe;
    }

    public async Task<int> GuardarAsync(
        FacturaStaging factura,
        string? nifProveedor,
        string? nombreProveedor,
        CancellationToken cancellationToken = default)
    {
        // Span de tot el desat (transacció inclosa). La primera petició després d'arrencar sol ser més
        // lenta perquè EF Core construeix el model i obre la primera connexió.
        using var activity = PersistenciaTracing.Source.StartActivity("bd.guardar_factura");
        activity?.SetTag("bd.lineas", factura.Lineas.Count);

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
        // Amb les incidències: s'apunten a l'historial les que quedaven pendents en aprovar
        var factura = await db.FacturasStaging
            .Include(f => f.Incidencias)
            .FirstOrDefaultAsync(f => f.Id == facturaId, cancellationToken);
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

        var ahora = timeProvider.GetUtcNow().UtcDateTime;
        factura.Estado = EstadoFactura.Validada;
        factura.FechaAprobacionManual = ahora;

        // "Aprovar igualment": queda constància de qui ho va aprovar, quan i amb quines incidències pendents
        var pendientes = string.Join(", ", factura.Incidencias.Select(i => i.Codigo).Distinct());
        db.HistorialCambiosFactura.Add(new HistorialCambioFactura
        {
            FacturaId = facturaId,
            Campo = "Estado",
            ValorAnterior = nameof(EstadoFactura.RevisionHumana),
            ValorNuevo = $"{nameof(EstadoFactura.Validada)} (aprobación manual)",
            FechaCambio = ahora,
            Usuario = UsuarioPorDefecto,
        });
        if (pendientes.Length > 0)
        {
            db.HistorialCambiosFactura.Add(new HistorialCambioFactura
            {
                FacturaId = facturaId,
                Campo = "Aprobada con incidencias",
                ValorAnterior = pendientes.Length <= 500 ? pendientes : pendientes[..500],
                ValorNuevo = null,
                FechaCambio = ahora,
                Usuario = UsuarioPorDefecto,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return ResultadoAprobacion.Ok();
    }

    public async Task<bool> ExisteOtraFacturaAsync(
        string nifEmisor, string numeroFactura, int excluirFacturaId, CancellationToken cancellationToken = default)
    {
        // Mateixa condició que ExisteFacturaAsync, excloent la factura que s'està editant
        using var activity = PersistenciaTracing.Source.StartActivity("bd.existe_otra_factura");
        return await db.FacturasStaging.AnyAsync(
            f => f.Id != excluirFacturaId
                && f.NumeroFactura == numeroFactura && f.Proveedor != null && f.Proveedor.Nif == nifEmisor
                && f.FechaEliminacion == null && f.Estado != EstadoFactura.Rechazada,
            cancellationToken);
    }

    public Task<FacturaStaging?> ObtenerParaEdicionAsync(int facturaId, CancellationToken cancellationToken = default) =>
        // Amb seguiment de canvis (sense AsNoTracking): el servei modifica aquesta mateixa instància i
        // GuardarEdicionAsync la desa. Proveedor fa falta per revalidar (NIF de l'emissor).
        db.FacturasStaging
            .Include(f => f.Proveedor)
            .Include(f => f.Lineas)
            .Include(f => f.Incidencias)
            .FirstOrDefaultAsync(f => f.Id == facturaId, cancellationToken);

    public async Task<IReadOnlyList<string>> ObtenerCamposEditadosAsync(int facturaId, CancellationToken cancellationToken = default) =>
        await db.HistorialCambiosFactura
            .Where(h => h.FacturaId == facturaId)
            .Select(h => h.Campo)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task GuardarEdicionAsync(
        FacturaStaging factura,
        IReadOnlyList<FacturaLinea> lineasEliminadas,
        IReadOnlyList<ValidacionIncidencia> incidenciasAnteriores,
        IReadOnlyList<HistorialCambioFactura> cambios,
        CancellationToken cancellationToken = default)
    {
        using var activity = PersistenciaTracing.Source.StartActivity("bd.guardar_edicion");

        // Tot o res: si falla qualsevol pas, no queda una edició a mitges
        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);

        // Les eliminacions es fan explícites (en lloc de dependre que EF detecti les línies "òrfenes"):
        // es veu clarament què s'esborra.
        db.FacturasLineas.RemoveRange(lineasEliminadas);
        db.ValidacionIncidencias.RemoveRange(incidenciasAnteriores);
        db.HistorialCambiosFactura.AddRange(cambios);

        // La resta (canvis de capçalera, línies modificades i línies noves afegides a factura.Lineas, incidències
        // noves) EF ho detecta sol perquè la factura es va llegir amb seguiment a ObtenerParaEdicionAsync.
        await db.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
    }

}
