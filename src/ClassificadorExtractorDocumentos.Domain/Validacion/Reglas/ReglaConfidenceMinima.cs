namespace ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;

/// <summary>CONFIDENCE_MINIMA: ningún campo obligatorio con confianza declarada &lt; 0.7
/// (mitigación de alucinaciones, SPEC §7). Solo aplica a campos presentes: los ausentes
/// (confidence 0) ya son Rechazo por CAMPOS_OBLIGATORIOS y no deben duplicar incidencia.</summary>
public class ReglaConfidenceMinima : IReglaValidacion
{
    public const double Umbral = 0.7;

    public string Codigo => "CONFIDENCE_MINIMA";

    public IEnumerable<Incidencia> Validar(ContextoValidacion contexto)
    {
        var f = contexto.Factura;
        var ausentes = f.CamposObligatoriosAusentes();

        foreach (var campo in Domain.Contracts.FacturaExtraida.CamposObligatorios)
        {
            if (!ausentes.Contains(campo) &&
                f.Metadatos.ConfidencePorCampo.TryGetValue(campo, out var confidence) &&
                confidence < Umbral)
            {
                yield return new Incidencia(Codigo,
                    $"El campo obligatorio '{campo}' tiene confianza {confidence:0.00} (mínimo {Umbral:0.00}).",
                    SeveridadIncidencia.Revision);
            }
        }
    }
}
