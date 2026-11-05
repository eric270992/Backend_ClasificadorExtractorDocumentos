namespace ClassificadorExtractorDocumentos.Domain.Entities;

/// <summary>
/// Un cambio individual sobre un campo de una <see cref="FacturaStaging"/>, hecho a mano por un
/// humano tras la extracción automática (validación o revisión). Una fila por campo modificado,
/// no una fila por edición: si se cambian IVA y Total a la vez, son dos filas con la misma fecha.
/// </summary>
public class HistorialCambioFactura
{
    public int Id { get; set; }
    public int FacturaId { get; set; }

    /// <summary>Nombre del campo modificado (p.ej. "CuotaIva", "NumeroFactura").</summary>
    public required string Campo { get; set; }

    /// <summary>Valor previo, como texto (formato invariante). Null si el campo no tenía valor.</summary>
    public string? ValorAnterior { get; set; }

    /// <summary>Valor nuevo, como texto (formato invariante). Null si se ha dejado vacío.</summary>
    public string? ValorNuevo { get; set; }

    public DateTime FechaCambio { get; set; }

    /// <summary>Quién hizo el cambio. Sin autenticación todavía en el sistema, de momento siempre
    /// un valor fijo ("Usuario desconocido"); cuando haya login se sustituirá por el usuario real.</summary>
    public required string Usuario { get; set; }
}
