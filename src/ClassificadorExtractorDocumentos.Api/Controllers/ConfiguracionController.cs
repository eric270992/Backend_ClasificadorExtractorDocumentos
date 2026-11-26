using ClassificadorExtractorDocumentos.Application.Grounding;
using ClassificadorExtractorDocumentos.Application.Ingesta;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ClassificadorExtractorDocumentos.Api.Controllers;

/// <summary>
/// Límits de pujada vigents, perquè el frontend els mostri sense tenir-los escrits a mà.
/// El frontend el crida en obrir la pantalla de documents: si es canvia la configuració del backend,
/// en tornar a entrar a l'aplicació ja surt el valor nou.
/// </summary>
[ApiController]
[Route("configuracion")]
public class ConfiguracionController(IngestaDocumentoService ingestaService, IOptions<GroundingOptions> groundingOptions) : ControllerBase
{
    [HttpGet]
    public IActionResult Obtener() => Ok(new
    {
        MaxPaginasPorDocumento = ingestaService.ObtenerMaxPaginasPorDocumento(),
        TamanoMaximoMb = DocumentosController.TamanoMaximoBytes / (1024 * 1024),
        GroundingHabilitado = groundingOptions.Value.Habilitado,
    });
}
