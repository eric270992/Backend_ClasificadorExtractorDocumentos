namespace ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;

/// <summary>
/// IVA_COHERENTE: verifica la cuota de IVA declarada, por orden de preferencia:
///   1. Si las líneas traen %IVA → cuotaIva ≈ Σ(base_línea × %IVA).
///   2. Si no, pero los totales indican un %IVA global → cuotaIva ≈ base × %global.
///   3. Si no hay ninguno de los dos y hay cuota ≠ 0 → no se puede verificar → Revisión.
/// Reverse charge o cuota nula/0 → no aplica (IVA 0 legítimo, lo cubre REVERSE_CHARGE_OK).
/// El cálculo vive en el modelo (FacturaExtraida); aquí solo la política.
/// </summary>
public class ReglaIvaCoherente : IReglaValidacion
{
    public string Codigo => "IVA_COHERENTE";

    public IEnumerable<Incidencia> Validar(ContextoValidacion contexto)
    {
        var f = contexto.Factura;
        var cuota = f.Totales.CuotaIva;

        // Nada que verificar: reverse charge (IVA 0 legítimo) o cuota ausente/cero
        if (f.Metadatos.ReverseCharge || cuota is null or 0m)
        {
            yield break;
        }

        // 1) IVA por línea (caso preferente). Si el cálculo da EXACTAMENTE 0 pese a que la cuota
        // declarada no es 0, no puede ser un 0% real por línea (si lo fuera, la cuota declarada
        // también sería 0): es el modelo rellenando porcentajeIva=0 por defecto al no venir indicado
        // por línea. Se descarta este caso y se cae al %IVA global en vez de un falso mismatch.
        var cuotaPorLineas = f.CuotaIvaCalculadaPorLineas();
        if (cuotaPorLineas is not null && cuotaPorLineas.Value != 0m)
        {
            if (Math.Abs(cuotaPorLineas.Value - cuota.Value) > contexto.ToleranciaCuadre)
            {
                yield return new Incidencia(Codigo,
                    $"La cuota de IVA calculada por líneas ({cuotaPorLineas:0.00}) no coincide con la declarada ({cuota:0.00}); diferencia {Math.Abs(cuotaPorLineas.Value - cuota.Value):0.00}.",
                    SeveridadIncidencia.Revision);
            }
            yield break;
        }

        // 2) %IVA global de los totales (las líneas no traen %)
        var cuotaPorGlobal = f.CuotaIvaEsperadaPorTipoGlobal();
        if (cuotaPorGlobal is not null)
        {
            if (Math.Abs(cuotaPorGlobal.Value - cuota.Value) > contexto.ToleranciaCuadre)
            {
                yield return new Incidencia(Codigo,
                    $"La cuota de IVA aplicando el tipo global {f.Totales.PorcentajeIva}% ({cuotaPorGlobal:0.00}) no coincide con la declarada ({cuota:0.00}); diferencia {Math.Abs(cuotaPorGlobal.Value - cuota.Value):0.00}.",
                    SeveridadIncidencia.Revision);
            }
            yield break;
        }

        // 3) Ni %IVA por línea ni %IVA global → no verificable
        yield return new Incidencia(Codigo,
            "No se puede verificar la coherencia del IVA: el documento no indica el % de IVA ni por línea ni en los totales.",
            SeveridadIncidencia.Revision);
    }
}
