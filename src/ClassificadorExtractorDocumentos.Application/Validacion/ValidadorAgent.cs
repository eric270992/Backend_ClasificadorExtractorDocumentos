using ClassificadorExtractorDocumentos.Domain.Entities;
using ClassificadorExtractorDocumentos.Domain.Validacion;

namespace ClassificadorExtractorDocumentos.Application.Validacion;

/// <summary>Agente Validador (SPEC §1.1): aplica todas las reglas §2.3 y deriva el estado final.
/// Validada · RevisionHumana (≥1 Revisión) · Rechazada (≥1 Rechazo). Las Info no penalizan.</summary>
public class ValidadorAgent(IEnumerable<IReglaValidacion> reglas)
{
    public ResultadoValidacion Validar(ContextoValidacion contexto)
    {
        var incidencias = reglas
            .SelectMany(r => r.Validar(contexto))
            .ToList();

        var estado = incidencias.Any(i => i.Severidad == SeveridadIncidencia.Rechazo)
            ? EstadoFactura.Rechazada
            : incidencias.Any(i => i.Severidad == SeveridadIncidencia.Revision)
                ? EstadoFactura.RevisionHumana
                : EstadoFactura.Validada;

        return new ResultadoValidacion(estado, incidencias);
    }
}

public sealed record ResultadoValidacion(EstadoFactura Estado, IReadOnlyList<Incidencia> Incidencias);
