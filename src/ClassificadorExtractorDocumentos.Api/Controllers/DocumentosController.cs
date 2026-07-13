using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Application.Ingesta;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ClassificadorExtractorDocumentos.Api.Controllers;

[ApiController]
[Route("documentos")]
public class DocumentosController(
    IIngestaPipeline orquestador,
    IngestaDocumentoService ingestaService,
    ExtractorAgent extractorAgent,
    IDocumentStorage documentStorage) : ControllerBase
{
    private const long TamanoMaximoBytes = 10 * 1024 * 1024;

    private static readonly string[] ExtensionesAdmitidas = [".pdf", ".jpg", ".jpeg", ".png"];

    /// <summary>Pipeline completo E1-F3: ingesta → extracción → validación → staging. Acepta PDF,
    /// JPEG y PNG (el formato real se valida por magic bytes en la ingesta).
    /// Con procesar=false solo ingesta (documento→imágenes en disco), útil para evals de extracción.
    /// El campo del formulario se mantiene 'pdf' por compatibilidad con los clientes existentes.</summary>
    [HttpPost]
    [RequestSizeLimit(TamanoMaximoBytes)]
    public async Task<IActionResult> Subir(IFormFile pdf, CancellationToken cancellationToken, [FromQuery] bool procesar = true)
    {
        if (pdf is null || pdf.Length == 0)
        {
            return BadRequest("Falta el fichero ('pdf').");
        }

        if (pdf.Length > TamanoMaximoBytes)
        {
            return BadRequest("El fichero supera el tamaño máximo permitido (10 MB).");
        }

        // Filtro amable por extensión (la validación fuerte por magic bytes ocurre en la ingesta)
        var extension = Path.GetExtension(pdf.FileName);
        if (extension.Length > 0 && !ExtensionesAdmitidas.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("Solo se admiten ficheros PDF, JPEG o PNG.");
        }

        await using var stream = pdf.OpenReadStream();

        try
        {
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
        catch (FormatoNoSoportadoException ex)
        {
            return BadRequest(ex.Message);
        }
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
