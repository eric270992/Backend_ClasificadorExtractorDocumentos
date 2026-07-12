namespace ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;

/// <summary>IVA_COHERENTE: cuotaIva ≈ Σ(base_línea × %IVA) cuando hay IVA por línea. Si las líneas
/// incluyen IVA, la base de línea se deriva: importe / (1 + %IVA).</summary>
public class ReglaIvaCoherente : IReglaValidacion
{
    public string Codigo => "IVA_COHERENTE";

    public IEnumerable<Incidencia> Validar(ContextoValidacion contexto)
    {
        var f = contexto.Factura;
        var cuotaCalculada = f.CuotaIvaCalculadaPorLineas();
        if (f.Totales.CuotaIva is null || cuotaCalculada is null)
        {
            yield break;
        }

        var diferencia = Math.Abs(cuotaCalculada.Value - f.Totales.CuotaIva.Value);
        if (diferencia > contexto.ToleranciaCuadre)
        {
            yield return new Incidencia(Codigo,
                $"La cuota de IVA calculada por líneas ({cuotaCalculada:0.00}) no coincide con la declarada ({f.Totales.CuotaIva:0.00}); diferencia {diferencia:0.00}.",
                SeveridadIncidencia.Revision);
        }
    }
}
