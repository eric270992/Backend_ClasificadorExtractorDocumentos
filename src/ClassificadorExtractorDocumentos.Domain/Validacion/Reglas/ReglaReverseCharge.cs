namespace ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;

/// <summary>REVERSE_CHARGE_OK: con inversión del sujeto pasivo, cuota IVA 0 es VÁLIDO (no genera
/// incidencia). Solo se informa si reverseCharge=true y la cuota NO es 0 (situación incoherente).</summary>
public class ReglaReverseCharge : IReglaValidacion
{
    public string Codigo => "REVERSE_CHARGE_OK";

    public IEnumerable<Incidencia> Validar(ContextoValidacion contexto)
    {
        var f = contexto.Factura;
        if (f.Metadatos.ReverseCharge && f.Totales.CuotaIva is not null && f.Totales.CuotaIva != 0m)
        {
            yield return new Incidencia(Codigo,
                $"El documento declara inversión del sujeto pasivo pero la cuota de IVA es {f.Totales.CuotaIva:0.00} (se esperaba 0).",
                SeveridadIncidencia.Info);
        }
    }
}
