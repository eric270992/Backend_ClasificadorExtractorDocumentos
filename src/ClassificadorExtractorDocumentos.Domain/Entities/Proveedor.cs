namespace ClassificadorExtractorDocumentos.Domain.Entities;

public class Proveedor
{
    public int Id { get; set; }
    public required string Nif { get; set; }
    public required string Nombre { get; set; }
    public DateTime FechaAlta { get; set; }

    public List<ProveedorEjemplo> Ejemplos { get; set; } = [];
    public List<FacturaStaging> Facturas { get; set; } = [];
}
