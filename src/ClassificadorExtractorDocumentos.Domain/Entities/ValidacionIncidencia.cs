namespace ClassificadorExtractorDocumentos.Domain.Entities;

public class ValidacionIncidencia
{
    public int Id { get; set; }
    public int FacturaId { get; set; }
    public FacturaStaging? Factura { get; set; }
    public required string Codigo { get; set; }
    public required string Detalle { get; set; }
    public DateTime FechaCreacion { get; set; }
}
