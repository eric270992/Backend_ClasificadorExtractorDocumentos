using ClassificadorExtractorDocumentos.Domain.ValueObjects;

namespace ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;

/// <summary>NIF_FORMATO: NIF/CIF/NIE español o VAT básico UE. El conocimiento del formato vive
/// en el value object <see cref="Nif"/>; esta regla solo aporta la política (severidad, mensaje).
/// NIF ausente no es asunto de esta regla (CAMPOS_OBLIGATORIOS).</summary>
public class ReglaNifFormato : IReglaValidacion
{
    public string Codigo => "NIF_FORMATO";

    public IEnumerable<Incidencia> Validar(ContextoValidacion contexto)
    {
        var f = contexto.Factura;

        if (f.Emisor.Nif is { } nifEmisor && !Nif.FormatoValido(nifEmisor))
        {
            yield return new Incidencia(Codigo,
                $"El NIF del emisor '{nifEmisor}' no tiene un formato válido (ES o VAT UE).",
                SeveridadIncidencia.Revision);
        }

        if (f.Receptor.Nif is { } nifReceptor && !Nif.FormatoValido(nifReceptor))
        {
            yield return new Incidencia(Codigo,
                $"El NIF del receptor '{nifReceptor}' no tiene un formato válido (ES o VAT UE).",
                SeveridadIncidencia.Revision);
        }
    }
}
