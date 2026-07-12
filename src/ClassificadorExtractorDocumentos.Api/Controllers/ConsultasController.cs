using ClassificadorExtractorDocumentos.Application.Consultor;
using Microsoft.AspNetCore.Mvc;

namespace ClassificadorExtractorDocumentos.Api.Controllers;

/// <summary>Agente Consultor: preguntas en lenguaje natural sobre el staging (SPEC §2.4, E1-F4).</summary>
[ApiController]
[Route("consultas")]
public class ConsultasController(ConsultorAgent consultor) : ControllerBase
{
    public sealed record PreguntaRequest(string Pregunta);

    [HttpPost]
    public async Task<IActionResult> Preguntar([FromBody] PreguntaRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Pregunta))
        {
            return BadRequest("Falta la pregunta ('pregunta').");
        }

        var r = await consultor.PreguntarAsync(request.Pregunta, cancellationToken);

        // La respuesta SIEMPRE incluye el SQL (criterio E1-F4: transparencia de lo ejecutado)
        return r.Exito
            ? Ok(new
            {
                r.Respuesta,
                r.Explicacion,
                r.Sql,
                NumFilas = r.Datos!.Filas.Count,
                r.Datos.Columnas,
                r.Datos.Filas,
            })
            : UnprocessableEntity(new { r.Motivo, SqlGenerado = r.Sql });
    }
}
