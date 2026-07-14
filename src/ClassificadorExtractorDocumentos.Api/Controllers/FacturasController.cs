using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ClassificadorExtractorDocumentos.Api.Controllers;

/// <summary>Consulta de estado e incidencias del staging (SPEC E1-F3, T3.3). El controlador NO toca la
/// base de datos: delega en <see cref="IFacturaConsultaService"/> (contrato de lectura).</summary>
[ApiController]
[Route("facturas")]
public class FacturasController(IFacturaConsultaService consultas) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var facturas = await consultas.ListarAsync(cancellationToken);
        return Ok(facturas);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancellationToken)
    {
        var factura = await consultas.ObtenerDetalleAsync(id, cancellationToken);
        return factura is null
            ? NotFound($"No existe ninguna factura con id {id}.")
            : Ok(factura);
    }
}
