namespace ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;

/// <summary>DUPLICADO: (proveedor + número de factura) ya existe en staging → Rechazo.
/// La consulta a BD la hace el orquestador; aquí llega precalculada en el contexto.</summary>
public class ReglaDuplicado : IReglaValidacion
{
    public string Codigo => "DUPLICADO";

    public IEnumerable<Incidencia> Validar(ContextoValidacion contexto)
    {
        if (contexto.ExisteDuplicado)
        {
            var f = contexto.Factura;
            yield return new Incidencia(Codigo,
                $"Ya existe en staging una factura del proveedor '{f.Emisor.Nif}' con número '{f.Factura.Numero}'.",
                SeveridadIncidencia.Rechazo);
        }
    }
}
