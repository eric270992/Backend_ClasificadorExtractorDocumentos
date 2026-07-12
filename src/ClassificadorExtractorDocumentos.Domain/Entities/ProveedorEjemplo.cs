namespace ClassificadorExtractorDocumentos.Domain.Entities;

/// <summary>Ejemplo few-shot validado de un proveedor. Se rellena en Etapa 2.</summary>
public class ProveedorEjemplo
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }
    public required string RutaImagen { get; set; }
    public required string JsonValidado { get; set; }
    public DateTime FechaCreacion { get; set; }
}
