using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Application.Ingesta;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ClassificadorExtractorDocumentos.Api.Controllers;

[ApiController]
[Route("documentos")]
public class DocumentosController(
    IngestaOrquestador orquestador,
    IngestaDocumentoService ingestaService,
    ExtractorAgent extractorAgent,
    IDocumentStorage documentStorage) : ControllerBase
{
    private const long TamanoMaximoBytes = 10 * 1024 * 1024;

    /// <summary>Pipeline completo E1-F3: ingesta → extracción → validación → staging.
    /// Con procesar=false solo ingesta (PDF→imágenes en disco), útil para evals de extracción.</summary>
    [HttpPost]
    [RequestSizeLimit(TamanoMaximoBytes)]
    public async Task<IActionResult> Subir(IFormFile pdf, CancellationToken cancellationToken, [FromQuery] bool procesar = true)
    {
        if (pdf is null || pdf.Length == 0)
        {
            return BadRequest("Falta el fichero PDF ('pdf').");
        }

        if (pdf.Length > TamanoMaximoBytes)
        {
            return BadRequest("El fichero supera el tamaño máximo permitido (10 MB).");
        }

        if (!string.Equals(pdf.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase) &&
            !Path.GetExtension(pdf.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Solo se admiten ficheros PDF.");
        }

        await using var stream = pdf.OpenReadStream();

        if (!procesar)
        {
            var documento = await ingestaService.IngestarAsync(stream, cancellationToken);
            return CreatedAtAction(nameof(Subir), new { id = documento.Id },
                new { DocumentoId = documento.Id, Paginas = documento.RutasImagenes.Count });
        }

        var resultado = await orquestador.ProcesarAsync(stream, cancellationToken);

        if (resultado.Error is not null)
        {
            return UnprocessableEntity(new { resultado.DocumentoId, resultado.Error });
        }

        return CreatedAtAction(nameof(Subir), new { id = resultado.DocumentoId }, new
        {
            resultado.DocumentoId,
            resultado.FacturaId,
            Estado = resultado.Estado!.ToString(),
            Incidencias = resultado.Incidencias.Select(i => new { i.Codigo, i.Detalle, Severidad = i.Severidad.ToString() }),
        });
    }

    /// <summary>Extracción sin persistir (herramienta de depuración de E1-F2).</summary>
    [HttpPost("{id:guid}/extraccion")]
    public async Task<IActionResult> Extraer(Guid id, CancellationToken cancellationToken)
    {
        var imagenes = await documentStorage.ObtenerImagenesAsync(id, cancellationToken);
        if (imagenes is null)
        {
            return NotFound($"No existe ningún documento con id {id}.");
        }

        var resultado = await extractorAgent.ExtraerAsync(imagenes, cancellationToken);

        return resultado.Exito
            ? Ok(new { resultado.Factura, resultado.Reintentos })
            : UnprocessableEntity(new { resultado.Error, resultado.Reintentos });
    }
}
