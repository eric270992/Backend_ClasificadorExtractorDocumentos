using System.Globalization;

namespace ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;

/// <summary>FECHA_RAZONABLE: fecha de factura no futura ni anterior a 10 años respecto a la
/// fecha de referencia del contexto.</summary>
public class ReglaFechaRazonable : IReglaValidacion
{
    public string Codigo => "FECHA_RAZONABLE";

    public IEnumerable<Incidencia> Validar(ContextoValidacion contexto)
    {
        var fechaTexto = contexto.Factura.Factura.Fecha;
        if (fechaTexto is null ||
            !DateOnly.TryParseExact(fechaTexto, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var fecha))
        {
            yield break; // ausencia → CAMPOS_OBLIGATORIOS; formato no ISO no debería llegar (parser normaliza)
        }

        if (fecha > contexto.FechaReferencia)
        {
            yield return new Incidencia(Codigo,
                $"La fecha de la factura ({fecha:yyyy-MM-dd}) es futura respecto a hoy ({contexto.FechaReferencia:yyyy-MM-dd}).",
                SeveridadIncidencia.Revision);
        }
        else if (fecha < contexto.FechaReferencia.AddYears(-10))
        {
            yield return new Incidencia(Codigo,
                $"La fecha de la factura ({fecha:yyyy-MM-dd}) es anterior a 10 años.",
                SeveridadIncidencia.Revision);
        }
    }
}
