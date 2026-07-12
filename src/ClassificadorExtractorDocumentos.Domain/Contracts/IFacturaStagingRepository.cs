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
}
