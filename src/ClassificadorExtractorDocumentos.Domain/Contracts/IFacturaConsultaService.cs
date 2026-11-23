namespace ClassificadorExtractorDocumentos.Domain.Contracts;

/// <summary>
/// Servicio de consulta (lectura) del staging de facturas. Es el contrato que usa la API para
/// listar y ver facturas SIN conocer EF Core ni el DbContext (el controlador ya no toca la BD).
/// En vocabulario clásico: sería la "IService"/"IDAO" de lectura; la implementación
/// (<see cref="Persistence"/>, en Infrastructure) es el adaptador que habla con la base de datos.
/// Separado del <see cref="IFacturaStagingRepository"/> de escritura (CQRS ligero: lecturas y
/// escrituras tienen contratos distintos).
/// </summary>
public interface IFacturaConsultaService
{
    Task<IReadOnlyList<FacturaResumen>> ListarAsync(CancellationToken cancellationToken = default);

    Task<FacturaDetalle?> ObtenerDetalleAsync(int id, CancellationToken cancellationToken = default);
}

// ── Modelos de lectura (DTOs) devueltos a la API. No son entidades EF: definen el contrato de salida.

public sealed record FacturaResumen(
    int Id,
    string? Proveedor,
    string? NifProveedor,
    string NumeroFactura,
    DateOnly FechaFactura,
    string Moneda,
    decimal Total,
    string Estado,
    int NumIncidencias,
    DateTime FechaIngesta);

public sealed record FacturaDetalle(
    int Id,
    ProveedorResumen? Proveedor,
    ClienteResumen? Cliente,
    string NumeroFactura,
    DateOnly FechaFactura,
    DateOnly? FechaVencimiento,
    string Moneda,
    decimal BaseImponible,
    decimal CuotaIva,
    decimal? RetencionIrpf,
    decimal Total,
    bool ReverseCharge,
    // Si els imports de línia ja porten l'IVA (tiquets) o no (factures). El frontend ho necessita per
    // saber si la suma de línies és el total o la base en recalcular la capçalera. No és una columna
    // de la taula: es llegeix del JSON original de l'extracció (com fa FacturaRevalidacionMapper).
    bool LineasIncluyenIva,
    string Estado,
    byte NivelExtraccion,
    DateTime FechaIngesta,
    DateTime? FechaAprobacionManual,
    IReadOnlyList<LineaResumen> Lineas,
    IReadOnlyList<IncidenciaResumen> Incidencias,
    IReadOnlyList<HistorialCambioResumen> Historial);

public sealed record ProveedorResumen(string Nif, string Nombre);

/// <summary>Receptor de la factura. A diferencia del proveedor no es una entidad con maestro, así que
/// cada campo puede faltar por separado (el PDF puede traer el nombre pero no el NIF, o al revés).</summary>
public sealed record ClienteResumen(string? Nif, string? Nombre);

/// <summary>Id: necessari perquè el frontend pugui dir quina línia modifica en editar.
/// PorcentajeIva null: el document no l'indicava per a aquesta línia.</summary>
public sealed record LineaResumen(
    int Id,
    int NumLinea,
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal? PorcentajeIva,
    decimal ImporteLinea);

public sealed record IncidenciaResumen(string Codigo, string Detalle, DateTime FechaCreacion);

/// <summary>Una edición manual de un campo, para pintar el historial de auditoría en el detalle.</summary>
public sealed record HistorialCambioResumen(
    string Campo,
    string? ValorAnterior,
    string? ValorNuevo,
    DateTime FechaCambio,
    string Usuario);
