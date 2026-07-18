using ClassificadorExtractorDocumentos.Domain.Entities;

namespace ClassificadorExtractorDocumentos.Domain.Contracts;

public interface IFacturaStagingRepository
{
    /// <summary>¿Existe ya en staging una factura del proveedor (por NIF) con ese número?</summary>
    Task<bool> ExisteFacturaAsync(string nifEmisor, string numeroFactura, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste la factura con sus líneas e incidencias en una única transacción (todo o nada),
    /// creando el proveedor por NIF si no existe. Devuelve el id de la factura.
    /// </summary>
    Task<int> GuardarAsync(
        FacturaStaging factura,
        string? nifProveedor,
        string? nombreProveedor,
        CancellationToken cancellationToken = default);

    /// <summary>Eliminación lógica (soft delete): marca FechaEliminacion, nunca borra la fila.
    /// Permite reprocesar (mismo proveedor+número) y una futura papelera. False si no existe.</summary>
    Task<bool> EliminarAsync(int facturaId, CancellationToken cancellationToken = default);

    /// <summary>Aprobación manual: solo válida si la factura está en RevisionHumana. Pasa a Validada
    /// y registra FechaAprobacionManual. Falla si no existe o no está en ese estado.</summary>
    Task<ResultadoAprobacion> AprobarAsync(int facturaId, CancellationToken cancellationToken = default);
}

public sealed record ResultadoAprobacion(bool Exito, string? Motivo)
{
    public static ResultadoAprobacion Ok() => new(true, null);
    public static ResultadoAprobacion Fallo(string motivo) => new(false, motivo);
}
