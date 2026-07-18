using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Application.Validacion;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Validacion;

namespace ClassificadorExtractorDocumentos.Application.Ingesta;

/// <summary>
/// Orquestador manual de Etapa 1 (patrón Orchestrator, SPEC §0.4): encadena
/// ingesta → Extractor → Validador → persistencia transaccional.
/// En Etapa 2 el pipeline por defecto pasa a ser <see cref="Maf.MafIngestaOrquestador"/>, pero
/// esta implementación se mantiene como plan B (SPEC §7): si MAF diera problemas, basta cambiar
/// el registro de <see cref="IIngestaPipeline"/> en la DI para volver aquí.
/// </summary>
public class IngestaOrquestador(
    IngestaDocumentoService ingestaService,
    ExtractorAgent extractorAgent,
    ValidadorAgent validadorAgent,
    IFacturaStagingRepository repositorio,
    TimeProvider timeProvider) : IIngestaPipeline
{
    public async Task<ResultadoIngesta> ProcesarAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        // 1. Ingesta: PDF → PNGs en disco
        var documento = await ingestaService.IngestarAsync(pdfStream, cancellationToken);

        // 2. Extracción (nivel 3 genérico en E1)
        var imagenes = await ingestaService.ObtenerImagenesAsync(documento.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Documento {documento.Id} recién ingestado sin imágenes.");
        var extraccion = await extractorAgent.ExtraerAsync(imagenes, cancellationToken);

        if (!extraccion.Exito)
        {
            // Error controlado: el documento queda en disco pero no entra en staging
            return ResultadoIngesta.ErrorExtraccion(documento.Id, extraccion.Error!);
        }

        var factura = extraccion.Factura!;

        // 3. Validación (el duplicado se precalcula aquí: las reglas son puras)
        var existeDuplicado = factura.Emisor.Nif is not null && factura.Factura.Numero is not null &&
            await repositorio.ExisteFacturaAsync(factura.Emisor.Nif, factura.Factura.Numero, cancellationToken);

        var contexto = ContextoValidacion.Crear(
            factura,
            existeDuplicado,
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
        var validacion = validadorAgent.Validar(contexto);

        // 4. Persistencia transaccional (todo o nada)
        var entidad = FacturaStagingMapper.Map(documento, extraccion, validacion, timeProvider.GetUtcNow().UtcDateTime);
        var facturaId = await repositorio.GuardarAsync(
            entidad, factura.Emisor.Nif, factura.Emisor.Nombre, cancellationToken);

        return ResultadoIngesta.Ok(documento.Id, facturaId, validacion.Estado, validacion.Incidencias);
    }
}
