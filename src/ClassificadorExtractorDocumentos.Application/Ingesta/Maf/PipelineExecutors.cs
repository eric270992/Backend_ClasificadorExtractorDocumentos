using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Application.Validacion;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Validacion;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace ClassificadorExtractorDocumentos.Application.Ingesta.Maf;

// Los executors del Workflow MAF. Cada uno ENVUELVE un agente existente (sin modificarlo):
// solo cambia quién los encadena. Los handlers devuelven su tipo de salida (auto-enrutado);
// las aristas condicionales del workflow deciden el camino según el resultado de la extracción.

/// <summary>Paso 1: extracción. Devuelve siempre el resultado (éxito o error); el desvío lo hacen
/// las aristas condicionales del workflow.</summary>
internal sealed partial class ExtractorExecutor(ExtractorAgent agente, ILogger logger)
    : Executor("extractor")
{
    [MessageHandler]
    private async ValueTask<ExtraccionRealizada> HandleAsync(DocumentoParaProcesar msg, IWorkflowContext context)
    {
        logger.LogInformation("[{CorrelationId}] Extracción iniciada", msg.Documento.Id);
        var extraccion = await agente.ExtraerAsync(msg.Imagenes);

        if (extraccion.Exito)
        {
            logger.LogInformation("[{CorrelationId}] Extracción OK ({Reintentos} reintentos)", msg.Documento.Id, extraccion.Reintentos);
        }
        else
        {
            logger.LogWarning("[{CorrelationId}] Extracción fallida: {Error}", msg.Documento.Id, extraccion.Error);
        }

        return new ExtraccionRealizada(msg.Documento, extraccion);
    }
}

/// <summary>Paso 2 (éxito): validación. Precalcula el duplicado (las reglas son puras) y aplica el Validador.</summary>
internal sealed partial class ValidadorExecutor(
    ValidadorAgent validador,
    IFacturaStagingRepository repositorio,
    TimeProvider timeProvider,
    ILogger logger) : Executor("validador")
{
    [MessageHandler]
    private async ValueTask<FacturaParaPersistir> HandleAsync(ExtraccionRealizada msg, IWorkflowContext context)
    {
        var factura = msg.Extraccion.Factura!;

        var existeDuplicado = factura.Emisor.Nif is not null && factura.Factura.Numero is not null &&
            await repositorio.ExisteFacturaAsync(factura.Emisor.Nif, factura.Factura.Numero);

        var contexto = ContextoValidacion.Crear(
            factura, existeDuplicado, DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
        var validacion = validador.Validar(contexto);

        logger.LogInformation("[{CorrelationId}] Validación → {Estado} ({N} incidencias)",
            msg.Documento.Id, validacion.Estado, validacion.Incidencias.Count);

        return new FacturaParaPersistir(msg.Documento, msg.Extraccion, validacion);
    }
}

/// <summary>Paso 3 (éxito): persistencia transaccional. Emite el ResultadoIngesta final del workflow.</summary>
[YieldsOutput(typeof(ResultadoIngesta))]
internal sealed partial class PersistenciaExecutor(
    IFacturaStagingRepository repositorio,
    TimeProvider timeProvider,
    ILogger logger) : Executor("persistencia")
{
    [MessageHandler]
    private async ValueTask HandleAsync(FacturaParaPersistir msg, IWorkflowContext context)
    {
        var factura = msg.Extraccion.Factura!;
        var entidad = FacturaStagingMapper.Map(
            msg.Documento, msg.Extraccion, msg.Validacion, timeProvider.GetUtcNow().UtcDateTime);

        var facturaId = await repositorio.GuardarAsync(entidad, factura.Emisor.Nif, factura.Emisor.Nombre);

        logger.LogInformation("[{CorrelationId}] Persistida como factura {FacturaId}", msg.Documento.Id, facturaId);
        await context.YieldOutputAsync(
            ResultadoIngesta.Ok(msg.Documento.Id, facturaId, msg.Validacion.Estado, msg.Validacion.Incidencias));
    }
}

/// <summary>Camino de error (extracción fallida): no persiste nada; emite el ResultadoIngesta de error.</summary>
[YieldsOutput(typeof(ResultadoIngesta))]
internal sealed partial class ErrorExtraccionExecutor(ILogger logger) : Executor("error-extraccion")
{
    [MessageHandler]
    private async ValueTask HandleAsync(ExtraccionRealizada msg, IWorkflowContext context)
    {
        logger.LogWarning("[{CorrelationId}] Documento no persistido por error de extracción", msg.Documento.Id);
        await context.YieldOutputAsync(
            ResultadoIngesta.ErrorExtraccion(msg.Documento.Id, msg.Extraccion.Error!));
    }
}
