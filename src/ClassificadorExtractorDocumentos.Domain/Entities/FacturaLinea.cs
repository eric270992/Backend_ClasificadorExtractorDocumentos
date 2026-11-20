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
    /// <summary>Null = el document no indica el %IVA d'aquesta línia (regla 11 del prompt). Abans es desava 0,
    /// però llavors no es podia distingir "no indicat" de "exempt (0%)" i en revalidar una edició manual
    /// les regles d'IVA ho haurien interpretat com un 0% real.</summary>
    public decimal? PorcentajeIva { get; set; }
    public decimal ImporteLinea { get; set; }
}
