using ClassificadorExtractorDocumentos.Domain.Entities;
using ClassificadorExtractorDocumentos.Domain.Validacion;

namespace ClassificadorExtractorDocumentos.Application.Ingesta;

/// <summary>
/// Pipeline de ingesta de un documento: ingesta → extracción → validación → persistencia.
/// Existen dos implementaciones intercambiables (SPEC §7, plan B de MAF):
///   · <see cref="IngestaOrquestador"/>       — orquestador manual (Etapa 1)
///   · <see cref="Maf.MafIngestaOrquestador"/> — Workflow de Microsoft Agent Framework (Etapa 2)
/// La activa se elige en la configuración de DI. Los agentes son idénticos en ambas.
/// </summary>
public interface IIngestaPipeline
{
    Task<ResultadoIngesta> ProcesarAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}

public sealed record ResultadoIngesta(
    Guid DocumentoId,
    int? FacturaId,
    EstadoFactura? Estado,
    IReadOnlyList<Incidencia> Incidencias,
    string? Error)
{
    public static ResultadoIngesta Ok(Guid documentoId, int facturaId, EstadoFactura estado, IReadOnlyList<Incidencia> incidencias) =>
        new(documentoId, facturaId, estado, incidencias, null);

    public static ResultadoIngesta ErrorExtraccion(Guid documentoId, string error) =>
        new(documentoId, null, null, [], error);
}
