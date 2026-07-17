namespace ClassificadorExtractorDocumentos.Domain.Contracts;

/// <summary>
/// Contrato de salida del agente Extractor (SPEC §2.1). Campo no encontrado = null + confidence 0.
/// El modelo responde preguntas (cálculos, presencia de campos); las reglas de validación
/// deciden la política (severidades, tolerancias, mensajes).
/// </summary>
public sealed record FacturaExtraida(
    EmisorExtraido Emisor,
    ReceptorExtraido Receptor,
    DatosFacturaExtraidos Factura,
    IReadOnlyList<LineaExtraida> Lineas,
    bool LineasIncluyenIva,
    TotalesExtraidos Totales,
    MetadatosExtraidos Metadatos)
{
    /// <summary>Los 4 campos sin los cuales el documento no es procesable como factura (§2.3).</summary>
    public static readonly IReadOnlyList<string> CamposObligatorios =
        ["emisor.nif", "factura.numero", "factura.fecha", "totales.total"];

    /// <summary>Suma de importes de línea, o null si no hay líneas o alguna no tiene importe.</summary>
    public decimal? SumaLineas() =>
        Lineas.Count == 0 || Lineas.Any(l => l.ImporteLinea is null)
            ? null
            : Lineas.Sum(l => l.ImporteLinea!.Value);

    /// <summary>Contra qué debe cuadrar la suma de líneas: base+IVA si los importes llevan IVA, base si no.
    /// Usa los totales efectivos (derivados de las líneas si el documento no los imprime).</summary>
    public decimal? ImporteObjetivoLineas() =>
        LineasIncluyenIva
            ? BaseImponibleEfectiva() + CuotaIvaEfectiva()
            : BaseImponibleEfectiva();

    /// <summary>Cuota de IVA que se deduce de las líneas (Σ base_línea × %IVA), o null si falta algún dato.</summary>
    public decimal? CuotaIvaCalculadaPorLineas() =>
        Lineas.Count == 0 || Lineas.Any(l => l.ImporteLinea is null || l.PorcentajeIva is null)
            ? null
            : Lineas.Sum(l => l.CuotaIva(LineasIncluyenIva)!.Value);

    /// <summary>Base imponible que se deduce de las líneas (Σ base_línea), o null si falta algún dato.</summary>
    public decimal? BaseImponibleCalculadaPorLineas() =>
        Lineas.Count == 0 || Lineas.Any(l => l.ImporteLinea is null || l.PorcentajeIva is null)
            ? null
            : Lineas.Sum(l => l.BaseImponible(LineasIncluyenIva)!.Value);

    /// <summary>Base imponible EFECTIVA: la del documento si la trae; si no, la deducida de las líneas.
    /// Permite cuadrar facturas que solo imprimen el total con IVA incluido (p. ej. plantilla B).</summary>
    public decimal? BaseImponibleEfectiva() =>
        Totales.BaseImponible ?? BaseImponibleCalculadaPorLineas();

    /// <summary>Cuota de IVA EFECTIVA: la del documento si la trae; si no, la deducida de las líneas.</summary>
    public decimal? CuotaIvaEfectiva() =>
        Totales.CuotaIva ?? CuotaIvaCalculadaPorLineas();

    /// <summary>Cuota de IVA esperada aplicando el %IVA global de los totales (cuando las líneas no lo
    /// traen). Usa la base imponible; si falta, la deriva del total. Null si no hay % global o no hay
    /// base ni total con los que calcular.</summary>
    public decimal? CuotaIvaEsperadaPorTipoGlobal()
    {
        if (Totales.PorcentajeIva is not { } pct || pct <= 0m)
        {
            return null;
        }

        var tasa = pct / 100m;
        if (Totales.BaseImponible is { } baseImp)
        {
            return baseImp * tasa;
        }
        if (Totales.Total is { } total)
        {
            // total = base × (1 + tasa) → cuota = total × tasa / (1 + tasa)
            return total * tasa / (1m + tasa);
        }
        return null;
    }

    /// <summary>Nombres de campos obligatorios ausentes en la extracción (vacío = factura procesable).</summary>
    public IReadOnlyList<string> CamposObligatoriosAusentes()
    {
        var ausentes = new List<string>();
        if (Emisor.Nif is null) ausentes.Add("emisor.nif");
        if (Factura.Numero is null) ausentes.Add("factura.numero");
        if (Factura.Fecha is null) ausentes.Add("factura.fecha");
        if (Totales.Total is null) ausentes.Add("totales.total");
        return ausentes;
    }
}

public sealed record EmisorExtraido(string? Nif, string? Nombre, string? Direccion);

public sealed record ReceptorExtraido(string? Nif, string? Nombre);

public sealed record DatosFacturaExtraidos(
    string? Numero,
    string? Fecha,
    string? Vencimiento,
    string Moneda = "EUR");

public sealed record LineaExtraida(
    string? Descripcion,
    decimal? Cantidad,
    decimal? PrecioUnitario,
    decimal? PorcentajeIva,
    decimal? ImporteLinea)
{
    /// <summary>Base imponible de la línea: el importe tal cual, o descontando el IVA si va incluido.</summary>
    public decimal? BaseImponible(bool importeIncluyeIva) =>
        importeIncluyeIva
            ? ImporteLinea / (1m + PorcentajeIva / 100m)
            : ImporteLinea;

    /// <summary>Cuota de IVA de la línea (base × %IVA), o null si falta importe o porcentaje.</summary>
    public decimal? CuotaIva(bool importeIncluyeIva) =>
        BaseImponible(importeIncluyeIva) * (PorcentajeIva / 100m);
}

public sealed record TotalesExtraidos(
    decimal? BaseImponible,
    decimal? CuotaIva,
    decimal? RetencionIrpf,
    decimal? Total,
    decimal? PorcentajeIva = null)
{
    /// <summary>Definición del total de una factura: base + cuota IVA − retención IRPF.
    /// Null si falta base o cuota (IRPF ausente cuenta como 0).</summary>
    public decimal? TotalCalculado => BaseImponible + CuotaIva - (RetencionIrpf ?? 0m);

    /// <summary>¿El documento aporta los tres importes necesarios para verificar el cuadre?</summary>
    public bool CuadreVerificable => BaseImponible is not null && CuotaIva is not null && Total is not null;
}

public sealed record MetadatosExtraidos(
    string? Idioma,
    bool ReverseCharge,
    IReadOnlyDictionary<string, double> ConfidencePorCampo);
