using ClassificadorExtractorDocumentos.Application.Edicion;
using ClassificadorExtractorDocumentos.Application.Grounding;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ClassificadorExtractorDocumentos.Api.Controllers;

/// <summary>Consulta de estado e incidencias del staging (SPEC E1-F3, T3.3), y las dos acciones
/// manuales sobre una factura ya procesada: aprobación (RevisionHumana → Validada) y eliminación
/// lógica. La lectura delega en <see cref="IFacturaConsultaService"/>; la escritura, en
/// <see cref="IFacturaStagingRepository"/>. El controlador no toca la base de datos directamente.</summary>
[ApiController]
[Route("facturas")]
public class FacturasController(
    IFacturaConsultaService consultas,
    IFacturaStagingRepository repositorio,
    FacturaEdicionService edicion,
    GroundingService grounding) : ControllerBase
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

    /// <summary>Edición manual de la cabecera y, si llegan, de las líneas (añadir, modificar, eliminar):
    /// solo válida si la factura está en Validada o RevisionHumana. Cada cambio queda en el historial de
    /// auditoría. Tras editar se vuelven a pasar las reglas: devuelve el nuevo estado y las incidencias.
    /// Si la edición provocaría un Rechazo (p. ej. número duplicado), no se guarda nada (400 con el motivo).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, ActualizarFacturaRequest request, CancellationToken cancellationToken)
    {
        // L'edició (capçalera i línies) i la revalidació les fa el servei; aquí només es tradueix a HTTP
        var resultado = await edicion.ActualizarAsync(id, request, cancellationToken);
        if (!resultado.Exito)
        {
            return BadRequest(resultado.Motivo);
        }

        // Es retorna l'estat i les incidències després de revalidar, perquè el frontend ho pugui mostrar
        return Ok(new
        {
            Estado = resultado.Estado!.Value.ToString(),
            resultado.HuboCambios,
            Incidencias = resultado.Incidencias.Select(i => new { i.Codigo, i.Detalle, Severidad = i.Severidad.ToString() }),
        });
    }

    /// <summary>Grounding (prova): on és cada camp de la factura dins el document, amb caixes normalitzades 0-1.
    /// metodo=hibrido (per defecte; PDF amb text, sense cost de model) o metodo=llm (model de visió; també fotos).</summary>
    [HttpGet("{id:int}/grounding")]
    public async Task<IActionResult> Grounding(int id, CancellationToken cancellationToken, [FromQuery] string metodo = GroundingService.MetodoHibrido)
    {
        if (!GroundingService.EsMetodoValido(metodo))
        {
            return BadRequest($"Método de grounding no válido: '{metodo}'. Usa '{GroundingService.MetodoHibrido}' o '{GroundingService.MetodoLlm}'.");
        }

        var resultado = await grounding.LocalizarAsync(id, metodo, cancellationToken);
        return resultado is null
            ? NotFound($"No existe ninguna factura con id {id} (o no se encuentra su documento).")
            : Ok(resultado);
    }

    /// <summary>Imatge PNG d'una pàgina del document (1 = primera), per pintar-hi les caixes al visor.
    /// Es serveix per id de factura (i no de document) perquè és l'únic id que coneix el frontend.</summary>
    [HttpGet("{id:int}/paginas/{pagina:int}")]
    public async Task<IActionResult> Pagina(int id, int pagina, CancellationToken cancellationToken)
    {
        var imagen = await grounding.ObtenerImagenPaginaAsync(id, pagina, cancellationToken);
        return imagen is null
            ? NotFound($"No existe la página {pagina} de la factura {id}.")
            : File(imagen, "image/png");
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
