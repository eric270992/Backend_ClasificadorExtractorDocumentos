namespace ClassificadorExtractorDocumentos.Domain.Entities;

public class FacturaLinea
{
    public int Id { get; set; }
    public int FacturaId { get; set; }
    public FacturaStaging? Factura { get; set; }
    public int NumLinea { get; set; }
    public required string Descripcion { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal PorcentajeIva { get; set; }
    public decimal ImporteLinea { get; set; }
}
