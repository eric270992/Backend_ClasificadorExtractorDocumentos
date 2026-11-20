using ClassificadorExtractorDocumentos.Domain.Entities;

namespace ClassificadorExtractorDocumentos.Domain.Contracts;

public interface IFacturaStagingRepository
{
    /// <summary>¿Existe ya en staging una factura del proveedor (por NIF) con ese número?</summary>
    Task<bool> ExisteFacturaAsync(string nifEmisor, string numeroFactura, CancellationToken cancellationToken = default);

    /// <summary>Com <see cref="ExisteFacturaAsync"/> però sense comptar la factura <paramref name="excluirFacturaId"/>.
    /// Cal per revalidar una edició: si no, la factura que s'està editant es trobaria a si mateixa i sortiria
    /// sempre com a duplicada.</summary>
    Task<bool> ExisteOtraFacturaAsync(
        string nifEmisor, string numeroFactura, int excluirFacturaId, CancellationToken cancellationToken = default);

    /// <summary>Factura amb proveïdor, línies i incidències, amb seguiment de canvis (EF) perquè el servei
    /// d'edició la pugui modificar i després desar-la amb <see cref="GuardarEdicionAsync"/>. Null si no existeix.</summary>
    Task<FacturaStaging?> ObtenerParaEdicionAsync(int facturaId, CancellationToken cancellationToken = default);

    /// <summary>Noms dels camps que algú ha editat a mà alguna vegada (segons l'historial). Serveix per saber
    /// quins camps ja ha verificat una persona en revalidar.</summary>
    Task<IReadOnlyList<string>> ObtenerCamposEditadosAsync(int facturaId, CancellationToken cancellationToken = default);

    /// <summary>Desa una edició en una sola transacció: els canvis de capçalera i de línies que el servei ha aplicat
    /// sobre la factura, les línies eliminades, la substitució de les incidències i l'historial.</summary>
    Task GuardarEdicionAsync(
        FacturaStaging factura,
        IReadOnlyList<FacturaLinea> lineasEliminadas,
        IReadOnlyList<ValidacionIncidencia> incidenciasAnteriores,
        IReadOnlyList<HistorialCambioFactura> cambios,
        CancellationToken cancellationToken = default);

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
    /// y registra FechaAprobacionManual. Falla si no existe o no está en ese estado.
    /// Queda registrada en el historial junto con las incidencias que seguían pendientes ("aprobar igualmente").
    /// Una Rechazada no se puede aprobar: sus incidencias impiden integrar la factura.</summary>
    Task<ResultadoAprobacion> AprobarAsync(int facturaId, CancellationToken cancellationToken = default);

}

public sealed record ResultadoAprobacion(bool Exito, string? Motivo)
{
    public static ResultadoAprobacion Ok() => new(true, null);
    public static ResultadoAprobacion Fallo(string motivo) => new(false, motivo);
}

/// <summary>Campos editables a mano por un humano tras la extracción automática. No incluye el
/// proveedor (cambiarlo implica buscar/crear otro maestro, fuera del alcance de esta edición).</summary>
public sealed record ActualizarFacturaRequest(
    string? NifCliente,
    string? NombreCliente,
    string NumeroFactura,
    DateOnly FechaFactura,
    DateOnly? FechaVencimiento,
    string Moneda,
    decimal BaseImponible,
    decimal CuotaIva,
    decimal? RetencionIrpf,
    decimal Total,
    bool ReverseCharge,
    IReadOnlyList<LineaEditada>? Lineas = null);

/// <summary>Una línia tal com la deixa l'usuari. Id null = línia nova. Les línies existents que no arribin a
/// la llista s'eliminen. L'ordre de la llista és el nou ordre (NumLinea 1..n).
/// <see cref="ActualizarFacturaRequest.Lineas"/> null vol dir "no tocar les línies" (compatibilitat).</summary>
public sealed record LineaEditada(
    int? Id,
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal? PorcentajeIva,
    decimal ImporteLinea);
