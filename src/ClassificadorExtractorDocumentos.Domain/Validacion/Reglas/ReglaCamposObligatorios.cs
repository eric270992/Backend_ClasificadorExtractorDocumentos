namespace ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;

/// <summary>CAMPOS_OBLIGATORIOS: nif emisor, número, fecha y total presentes. Su ausencia
/// es Rechazo (el documento no es procesable como factura).</summary>
public class ReglaCamposObligatorios : IReglaValidacion
{
    public string Codigo => "CAMPOS_OBLIGATORIOS";

    public IEnumerable<Incidencia> Validar(ContextoValidacion contexto)
    {
        var ausentes = contexto.Factura.CamposObligatoriosAusentes();

        if (ausentes.Count > 0)
        {
            yield return new Incidencia(Codigo,
                $"Faltan campos obligatorios: {string.Join(", ", ausentes)}.",
                SeveridadIncidencia.Rechazo);
        }
    }
}
