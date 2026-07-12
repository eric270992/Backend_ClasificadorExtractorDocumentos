namespace ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;

/// <summary>CUADRE_TOTAL: base + cuotaIva − retencionIrpf ≈ total ±tolerancia. Si base o cuota no
/// vienen en el documento pero sí el total, no se puede verificar el cuadre → Revisión (caso
/// plantilla B: solo se imprime el total con IVA incluido).</summary>
public class ReglaCuadreTotal : IReglaValidacion
{
    public string Codigo => "CUADRE_TOTAL";

    public IEnumerable<Incidencia> Validar(ContextoValidacion contexto)
    {
        var t = contexto.Factura.Totales;
        if (t.Total is null)
        {
            yield break; // total ausente lo cubre CAMPOS_OBLIGATORIOS
        }

        if (!t.CuadreVerificable)
        {
            yield return new Incidencia(Codigo,
                "El documento no muestra base imponible y/o cuota de IVA: el cuadre no es verificable.",
                SeveridadIncidencia.Revision);
            yield break;
        }

        var diferencia = Math.Abs(t.TotalCalculado!.Value - t.Total.Value);
        if (diferencia > contexto.ToleranciaCuadre)
        {
            yield return new Incidencia(Codigo,
                $"base + IVA − IRPF = {t.TotalCalculado:0.00} no cuadra con el total {t.Total:0.00}; diferencia {diferencia:0.00}.",
                SeveridadIncidencia.Revision);
        }
    }
}
