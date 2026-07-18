using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ClassificadorExtractorDocumentos.Api.Controllers;

/// <summary>Consulta de estado e incidencias del staging (SPEC E1-F3, T3.3), y las dos acciones
/// manuales sobre una factura ya procesada: aprobación (RevisionHumana → Validada) y eliminación
/// lógica. La lectura delega en <see cref="IFacturaConsultaService"/>; la escritura, en
/// <see cref="IFacturaStagingRepository"/>. El controlador no toca la base de datos directamente.</summary>
[ApiController]
[Route("facturas")]
public class FacturasController(IFacturaConsultaService consultas, IFacturaStagingRepository repositorio) : ControllerBase
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

    /// <summary>Aprobación manual: solo válida si la factura está en RevisionHumana.</summary>
    [HttpPost("{id:int}/aprobar")]
    public async Task<IActionResult> Aprobar(int id, CancellationToken cancellationToken)
    {
        var resultado = await repositorio.AprobarAsync(id, cancellationToken);
        return resultado.Exito ? NoContent() : BadRequest(resultado.Motivo);
    }

    /// <summary>Eliminación lógica (soft delete): la factura deja de aparecer en el listado,
    /// pero no se borra físicamente (permite reprocesar el mismo proveedor+número).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken cancellationToken)
    {
        var eliminada = await repositorio.EliminarAsync(id, cancellationToken);
        return eliminada ? NoContent() : NotFound($"No existe ninguna factura con id {id}.");
    }
}
