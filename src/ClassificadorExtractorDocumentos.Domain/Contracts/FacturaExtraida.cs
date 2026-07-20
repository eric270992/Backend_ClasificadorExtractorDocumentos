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

    /// <summary>Tipos de IVA vigentes en España (%). Un tipo global DEDUCIDO (cuota/base) que no se
    /// acerque a ninguno de estos no es un tipo válido, sino señal de que base o cuota están mal
    /// extraídas: en ese caso no se deduce nada (la factura va a revisión).</summary>
    public static readonly IReadOnlyList<decimal> TiposIvaEspana = [0m, 4m, 10m, 21m];

    private const decimal ToleranciaTipoIva = 0.5m; // ±0,5 puntos porcentuales al encajar un tipo real

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

    /// <summary>%IVA global EFECTIVO: el declarado en los totales si lo trae; si no, el que se deduce de
    /// cuota/base SOLO cuando coincide (±0,5 pp) con un tipo real español (ver <see cref="TiposIvaEspana"/>).
    /// Motivación: modelos pequeños/gratuitos a veces no capturan el "(21%)" impreso aunque sí extraen
    /// bien cuota y base; en ese caso el tipo se puede recuperar sin inventar. Un cociente que no cae en
    /// ningún tipo real devuelve null (no se fuerza nada: la factura va a revisión).</summary>
    public decimal? PorcentajeIvaGlobalEfectivo()
    {
        if (Totales.PorcentajeIva is { } declarado && declarado > 0m)
        {
            return declarado;
        }

        if (BaseImponibleEfectiva() is not { } baseImp || baseImp == 0m ||
            CuotaIvaEfectiva() is not { } cuota)
        {
            return null;
        }

        var derivado = cuota / baseImp * 100m;
        var candidato = TiposIvaEspana.FirstOrDefault(t => Math.Abs(t - derivado) <= ToleranciaTipoIva, -1m);
        return candidato >= 0m ? candidato : null;
    }

    /// <summary>Cuota de IVA esperada aplicando el %IVA global EFECTIVO (declarado o deducido de un tipo
    /// real). Usa la base imponible; si falta, la deriva del total. Null si no hay % o no hay base ni
    /// total con los que calcular.</summary>
    public decimal? CuotaIvaEsperadaPorTipoGlobal()
    {
        if (PorcentajeIvaGlobalEfectivo() is not { } pct || pct <= 0m)
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

    /// <summary>Valor efectivo de "LineasIncluyenIva" a usar en las reglas: el declarado por el modelo,
    /// o el contrario si ESE no es internamente consistente (no cuadra Σlíneas ni la cuota de IVA) pero
    /// el contrario sí lo es. El modelo confunde este flag de vez en cuando (visto con varios
    /// proveedores LLM) — solo se autocorrige cuando hay señal suficiente para hacerlo con confianza;
    /// si ninguna de las dos interpretaciones cuadra, se respeta lo declarado.</summary>
    public bool LineasIncluyenIvaEfectivo(decimal toleranciaCuadre)
    {
        if (EsConsistente(this))
        {
            return LineasIncluyenIva;
        }

        var contraria = this with { LineasIncluyenIva = !LineasIncluyenIva };
        return EsConsistente(contraria) ? contraria.LineasIncluyenIva : LineasIncluyenIva;

        bool EsConsistente(FacturaExtraida f)
        {
            var suma = f.SumaLineas();
            var objetivo = f.ImporteObjetivoLineas();
            if (suma is null || objetivo is null || Math.Abs(suma.Value - objetivo.Value) > toleranciaCuadre)
            {
                return false;
            }

            var cuotaPorLineas = f.CuotaIvaCalculadaPorLineas();
            var cuotaDeclarada = f.Totales.CuotaIva;
            return cuotaPorLineas is null || cuotaDeclarada is null
                || Math.Abs(cuotaPorLineas.Value - cuotaDeclarada.Value) <= toleranciaCuadre;
        }
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
    /// <summary>PorcentajeIva normalizado: el modelo a veces confunde "21" (formato esperado) con
    /// "0.21" (fracción). Ningún tipo de IVA real está entre 0 y 1 (los tipos son 21, 10, 4, 0), así
    /// que un valor en ese rango se interpreta como fracción y se multiplica ×100.</summary>
    private decimal? PorcentajeIvaNormalizado =>
        PorcentajeIva is > 0m and < 1m ? PorcentajeIva * 100m : PorcentajeIva;

    /// <summary>Base imponible de la línea: el importe tal cual, o descontando el IVA si va incluido.</summary>
    public decimal? BaseImponible(bool importeIncluyeIva) =>
        importeIncluyeIva
            ? ImporteLinea / (1m + PorcentajeIvaNormalizado / 100m)
            : ImporteLinea;

    /// <summary>Cuota de IVA de la línea (base × %IVA), o null si falta importe o porcentaje.</summary>
    public decimal? CuotaIva(bool importeIncluyeIva) =>
        BaseImponible(importeIncluyeIva) * (PorcentajeIvaNormalizado / 100m);
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
