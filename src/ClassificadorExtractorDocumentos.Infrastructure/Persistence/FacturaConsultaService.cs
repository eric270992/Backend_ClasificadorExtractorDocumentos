using System.Text.Json;
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
        // La consulta retorna dues coses: el detall i el JSON original de l'extracció.
        // LineasIncluyenIva no és una columna: viu dins el JSON, i SQL no el pot llegir dins la projecció.
        // Per això el detall surt de la BD amb un valor provisional (false) i es completa just després.
        var resultadoConsulta = await db.FacturasStaging
            .AsNoTracking()
            .Where(f => f.Id == id && f.FechaEliminacion == null)
            .Select(f => new
            {
                JsonOriginal = f.JsonOriginalExtraido,
                Detalle = new FacturaDetalle(
                    f.Id,
                    f.Proveedor != null ? new ProveedorResumen(f.Proveedor.Nif, f.Proveedor.Nombre) : null,
                    // Sin cliente en absoluto → null (el front pinta "—"); con uno de los dos campos, se devuelve
                    f.NifCliente != null || f.NombreCliente != null
                        ? new ClienteResumen(f.NifCliente, f.NombreCliente)
                        : null,
                    f.NumeroFactura,
                    f.FechaFactura,
                    f.FechaVencimiento,
                    f.Moneda,
                    f.BaseImponible,
                    f.CuotaIva,
                    f.RetencionIrpf,
                    f.Total,
                    f.ReverseCharge,
                    false, // provisional: es substitueix pel valor del JSON (veure més avall)
                    f.Estado.ToString(),
                    f.NivelExtraccion,
                    f.FechaIngesta,
                    f.FechaAprobacionManual,
                    f.Lineas
                        .OrderBy(l => l.NumLinea)
                        .Select(l => new LineaResumen(
                            l.Id, l.NumLinea, l.Descripcion, l.Cantidad, l.PrecioUnitario, l.PorcentajeIva, l.ImporteLinea))
                        .ToList(),
                    f.Incidencias
                        .Select(i => new IncidenciaResumen(i.Codigo, i.Detalle, i.FechaCreacion))
                        .ToList(),
                    db.HistorialCambiosFactura
                        .Where(h => h.FacturaId == f.Id)
                        .OrderByDescending(h => h.FechaCambio)
                        .Select(h => new HistorialCambioResumen(h.Campo, h.ValorAnterior, h.ValorNuevo, h.FechaCambio, h.Usuario))
                        .ToList())
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (resultadoConsulta is null)
        {
            return null;
        }

        // "with" crea una còpia del detall canviant només aquest camp (els records són immutables)
        var lineasIncluyenIva = LeerLineasIncluyenIva(resultadoConsulta.JsonOriginal);
        return resultadoConsulta.Detalle with { LineasIncluyenIva = lineasIncluyenIva };
    }

    /// <summary>Llegeix "lineasIncluyenIva" del JSON original de l'extracció.
    /// Si el camp no hi és, no és true o el JSON no es pot llegir, retorna false: és el mateix valor per
    /// defecte que fa servir FacturaExtraidaParser en la ingesta, així el detall i les regles coincideixen.</summary>
    private static bool LeerLineasIncluyenIva(string jsonOriginal)
    {
        try
        {
            using var documentoJson = JsonDocument.Parse(jsonOriginal);
            var raiz = documentoJson.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object &&
                raiz.TryGetProperty("lineasIncluyenIva", out var valor) &&
                valor.ValueKind == JsonValueKind.True)
            {
                return true;
            }
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
