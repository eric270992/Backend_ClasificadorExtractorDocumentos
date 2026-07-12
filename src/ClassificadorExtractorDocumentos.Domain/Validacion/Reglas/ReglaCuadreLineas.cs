namespace ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;

/// <summary>CUADRE_LINEAS: Σ(importeLinea) ≈ baseImponible ±tolerancia. Si las líneas incluyen IVA,
/// la suma se compara contra base+cuota (el importe con IVA). Sin datos suficientes, no opina.</summary>
public class ReglaCuadreLineas : IReglaValidacion
{
    public string Codigo => "CUADRE_LINEAS";

    public IEnumerable<Incidencia> Validar(ContextoValidacion contexto)
    {
        var f = contexto.Factura;
        var suma = f.SumaLineas();
        var objetivo = f.ImporteObjetivoLineas();
        if (suma is null || objetivo is null)
        {
            yield break;
        }

        var diferencia = Math.Abs(suma.Value - objetivo.Value);
        if (diferencia > contexto.ToleranciaCuadre)
        {
            yield return new Incidencia(Codigo,
                $"La suma de líneas ({suma:0.00}) no cuadra con {(f.LineasIncluyenIva ? "base+IVA" : "la base imponible")} ({objetivo:0.00}); diferencia {diferencia:0.00}.",
                SeveridadIncidencia.Revision);
        }
    }
}
