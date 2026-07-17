namespace ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;

/// <summary>
/// CUADRE_TOTAL: base + cuotaIva − retencionIrpf ≈ total ±tolerancia. Usa los totales EFECTIVOS: si el
/// documento no imprime base y/o cuota (p. ej. plantilla B, que solo muestra el total con IVA incluido),
/// se deducen de las líneas (importe + %IVA). Solo si tampoco se pueden deducir → no verificable.
/// </summary>
public class ReglaCuadreTotal : IReglaValidacion
{
    public string Codigo => "CUADRE_TOTAL";

    public IEnumerable<Incidencia> Validar(ContextoValidacion contexto)
    {
        var f = contexto.Factura;
        var t = f.Totales;
        if (t.Total is null)
        {
            yield break; // total ausente lo cubre CAMPOS_OBLIGATORIOS
        }

        var baseEf = f.BaseImponibleEfectiva();
        var cuotaEf = f.CuotaIvaEfectiva();

        // Ni el documento ni las líneas permiten obtener base y cuota → no verificable
        if (baseEf is null || cuotaEf is null)
        {
            yield return new Incidencia(Codigo,
                "El documento no muestra base imponible y/o cuota de IVA, y no se pueden deducir de las líneas: el cuadre no es verificable.",
                SeveridadIncidencia.Revision);
            yield break;
        }

        var calculado = baseEf.Value + cuotaEf.Value - (t.RetencionIrpf ?? 0m);
        var diferencia = Math.Abs(calculado - t.Total.Value);
        if (diferencia > contexto.ToleranciaCuadre)
        {
            var deducido = t.BaseImponible is null || t.CuotaIva is null
                ? " (base/cuota deducidas de las líneas)"
                : "";
            yield return new Incidencia(Codigo,
                $"base + IVA − IRPF = {calculado:0.00} no cuadra con el total {t.Total:0.00}; diferencia {diferencia:0.00}{deducido}.",
                SeveridadIncidencia.Revision);
        }
    }
}
