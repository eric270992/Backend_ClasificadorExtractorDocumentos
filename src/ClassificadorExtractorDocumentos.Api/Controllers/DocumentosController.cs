using ClassificadorExtractorDocumentos.Api.DTOs;
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
    /// <summary>Pública perquè ConfiguracionController la pugui exposar al frontend. Ha de ser constant
    /// perquè la fa servir l'atribut [RequestSizeLimit].</summary>
    public const long TamanoMaximoBytes = 10 * 1024 * 1024;

    private static readonly string[] ExtensionesAdmitidas = [".pdf", ".jpg", ".jpeg", ".png"];

    /// <summary>Pipeline completo E1-F3: ingesta → extracción → validación → staging. Acepta PDF,
    /// JPEG y PNG (el formato real se valida por magic bytes en la ingesta).
    /// Con procesar=false solo ingesta (documento→imágenes en disco), útil para evals de extracción.
    /// El campo del formulario se mantiene 'pdf' por compatibilidad con los clientes existentes.</summary>
    [HttpPost]
    [RequestSizeLimit(TamanoMaximoBytes)]
    // OpenAPI només admet un tipus per codi d'estat (si en declares dos es queda amb l'últim), així que aquí
    // va el format habitual de cada codi i els alternatius (DTODocumentoIngestado i DTODocumentoDemasiadoLargo)
    // s'expliquen a la descripció.
    [EndpointDescription(
        "Amb procesar=false només fa la ingesta i el 201 retorna { documentoId, paginas } en lloc del resultat " +
        "complet. El 422 també pot ser { error, paginas, maxPaginas } si el document té més pàgines de les permeses.")]
    [ProducesResponseType<DTOFacturaProcesada>(StatusCodes.Status201Created)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<DTOErrorProcesado>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Subir(
        IFormFile pdf,
        CancellationToken cancellationToken,
        [FromQuery] bool procesar = true,
        [FromQuery] bool? usarTextoPdf = null)
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
                var documento = await ingestaService.IngestarAsync(stream, usarTextoPdf, cancellationToken);
                return CreatedAtAction(nameof(Subir), new { id = documento.Id },
                    new DTODocumentoIngestado(documento.Id, documento.RutasImagenes.Count));
            }

            var resultado = await orquestador.ProcesarAsync(stream, usarTextoPdf, cancellationToken);

            if (resultado.Error is not null)
            {
                return UnprocessableEntity(new DTOErrorProcesado(resultado.DocumentoId, resultado.Error));
            }

            return CreatedAtAction(nameof(Subir), new { id = resultado.DocumentoId }, new DTOFacturaProcesada(
                resultado.DocumentoId,
                resultado.FacturaId,
                resultado.Estado!.Value.ToString(),
                resultado.Incidencias.Select(i => new DTOIncidencia(i.Codigo, i.Detalle, i.Severidad.ToString()))));
        }
        catch (FormatoNoSoportadoException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (DocumentoDemasiadoLargoException ex)
        {
            // 422: el fitxer és correcte però no el podem processar. Mateix format { error } que la resta
            // d'errors de pujada, més les dues xifres perquè el frontend actualitzi el límit si ha canviat.
            return UnprocessableEntity(new DTODocumentoDemasiadoLargo(ex.Message, ex.Paginas, ex.MaxPaginas));
        }
    }

    /// <summary>Extracción sin persistir (herramienta de depuración de E1-F2).</summary>
    [HttpPost("{id:guid}/extraccion")]
    [ProducesResponseType<DTOExtraccion>(StatusCodes.Status200OK)]
    [ProducesResponseType<string>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<DTOErrorExtraccion>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Extraer(Guid id, CancellationToken cancellationToken)
    {
        var imagenes = await documentStorage.ObtenerImagenesAsync(id, cancellationToken);
        if (imagenes is null)
        {
            return NotFound($"No existe ningún documento con id {id}.");
        }

        var resultado = await extractorAgent.ExtraerAsync(imagenes, textoPdf: null, cancellationToken);

        return resultado.Exito
            ? Ok(new DTOExtraccion(resultado.Factura, resultado.Reintentos))
            : UnprocessableEntity(new DTOErrorExtraccion(resultado.Error, resultado.Reintentos));
    }
}
