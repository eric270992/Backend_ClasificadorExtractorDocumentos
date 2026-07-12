namespace ClassificadorExtractorDocumentos.Domain.Entities;

public class FacturaStaging
{
    public int Id { get; set; }
    public int? ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }
    public required string NumeroFactura { get; set; }
    public DateOnly FechaFactura { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public string Moneda { get; set; } = "EUR";
    public decimal BaseImponible { get; set; }
    public decimal CuotaIva { get; set; }
    public decimal? RetencionIrpf { get; set; }
    public decimal Total { get; set; }
    public bool ReverseCharge { get; set; }
    public EstadoFactura Estado { get; set; } = EstadoFactura.PendienteValidacion;
    public required string JsonOriginalExtraido { get; set; }

    /// <summary>2 = few-shot (E2), 3 = genérica. En E1 siempre 3.</summary>
    public byte NivelExtraccion { get; set; }
    public required string RutaPdfOriginal { get; set; }
    public DateTime FechaIngesta { get; set; }

    public List<FacturaLinea> Lineas { get; set; } = [];
    public List<ValidacionIncidencia> Incidencias { get; set; } = [];
}
