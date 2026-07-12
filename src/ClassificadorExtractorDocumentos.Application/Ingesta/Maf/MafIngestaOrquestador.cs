using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Application.Validacion;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace ClassificadorExtractorDocumentos.Application.Ingesta.Maf;

/// <summary>
/// Pipeline de ingesta implementado como Workflow de Microsoft Agent Framework (Etapa 2, E2-S1).
/// La ingesta (PDF→imágenes) se hace antes; el Workflow encadena Extractor → Validador → Persistencia.
/// Los agentes son los MISMOS que usaba el orquestador manual: aquí solo cambia quién los encadena.
/// </summary>
public class MafIngestaOrquestador(
    IngestaDocumentoService ingestaService,
    ExtractorAgent extractorAgent,
    ValidadorAgent validadorAgent,
    IFacturaStagingRepository repositorio,
    TimeProvider timeProvider,
    ILogger<MafIngestaOrquestador> logger) : IIngestaPipeline
{
    public async Task<ResultadoIngesta> ProcesarAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        // 1. Ingesta previa al workflow (I/O de preparación): PDF → PNGs en disco
        var documento = await ingestaService.IngestarAsync(pdfStream, cancellationToken);
        var imagenes = await ingestaService.ObtenerImagenesAsync(documento.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Documento {documento.Id} recién ingestado sin imágenes.");

        // 2. Construir el workflow con executors que envuelven los agentes (dependencias scoped).
        //    Arista condicional tras el Extractor: éxito → Validador; error → executor de error.
        var extractor = new ExtractorExecutor(extractorAgent, logger);
        var validador = new ValidadorExecutor(validadorAgent, repositorio, timeProvider, logger);
        var persistencia = new PersistenciaExecutor(repositorio, timeProvider, logger);
        var errorExtraccion = new ErrorExtraccionExecutor(logger);

        var workflow = new WorkflowBuilder(extractor)
            .AddEdge(extractor, validador, condition: ExtraccionCon(exito: true))
            .AddEdge(extractor, errorExtraccion, condition: ExtraccionCon(exito: false))
            .AddEdge(validador, persistencia)
            .WithOutputFrom(persistencia, errorExtraccion)
            .Build();

        logger.LogInformation("[{CorrelationId}] Workflow MAF iniciado", documento.Id);

        // 3. Ejecutar in-process y recoger el ResultadoIngesta emitido (por Persistencia en éxito,
        //    o por Extractor en caso de error de extracción)
        var run = await InProcessExecution.RunAsync(
            workflow, new DocumentoParaProcesar(documento, imagenes));

        foreach (var evt in run.NewEvents)
        {
            if (evt is WorkflowOutputEvent salida && salida.Data is ResultadoIngesta resultado)
            {
                return resultado;
            }
            if (evt is WorkflowErrorEvent error)
            {
                logger.LogError("[{CorrelationId}] Error en el workflow MAF: {Data}", documento.Id, error.Data);
            }
        }

        // Ningún output: no debería pasar; se trata como error controlado
        throw new InvalidOperationException(
            $"El workflow MAF terminó sin emitir un ResultadoIngesta (documento {documento.Id}).");
    }

    /// <summary>Condición de arista según el resultado de la extracción.</summary>
    private static Func<object?, bool> ExtraccionCon(bool exito) =>
        mensaje => mensaje is ExtraccionRealizada r && r.Extraccion.Exito == exito;
}
