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
    string NumeroFactura,
    DateOnly FechaFactura,
    DateOnly? FechaVencimiento,
    string Moneda,
    decimal BaseImponible,
    decimal CuotaIva,
    decimal? RetencionIrpf,
    decimal Total,
    bool ReverseCharge,
    string Estado,
    byte NivelExtraccion,
    DateTime FechaIngesta,
    DateTime? FechaAprobacionManual,
    IReadOnlyList<LineaResumen> Lineas,
    IReadOnlyList<IncidenciaResumen> Incidencias);

public sealed record ProveedorResumen(string Nif, string Nombre);

public sealed record LineaResumen(
    int NumLinea,
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal PorcentajeIva,
    decimal ImporteLinea);

public sealed record IncidenciaResumen(string Codigo, string Detalle, DateTime FechaCreacion);
