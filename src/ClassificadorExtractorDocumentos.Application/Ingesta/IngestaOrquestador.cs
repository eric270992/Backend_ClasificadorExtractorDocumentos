using System.Globalization;
using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Application.Validacion;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Entities;
using ClassificadorExtractorDocumentos.Domain.Validacion;

namespace ClassificadorExtractorDocumentos.Application.Ingesta;

/// <summary>
/// Orquestador manual de Etapa 1 (patrón Orchestrator, SPEC §0.4): encadena
/// ingesta → Extractor → Validador → persistencia transaccional. En E2-S1 será
/// sustituido por un Workflow de Microsoft Agent Framework sin tocar los agentes.
/// </summary>
public class IngestaOrquestador(
    IngestaDocumentoService ingestaService,
    ExtractorAgent extractorAgent,
    ValidadorAgent validadorAgent,
    IFacturaStagingRepository repositorio,
    TimeProvider timeProvider)
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

        var contexto = new ContextoValidacion(
            factura,
            existeDuplicado,
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
        var validacion = validadorAgent.Validar(contexto);

        // 4. Persistencia transaccional (todo o nada). Los campos NOT NULL de staging reciben
        // placeholders documentados cuando la extracción devolvió null (el estado Rechazada y
        // JsonOriginalExtraido conservan la verdad del documento).
        var entidad = MapearAEntidad(documento, extraccion, validacion);
        var facturaId = await repositorio.GuardarAsync(
            entidad, factura.Emisor.Nif, factura.Emisor.Nombre, cancellationToken);

        return ResultadoIngesta.Ok(documento.Id, facturaId, validacion.Estado, validacion.Incidencias);
    }

    private FacturaStaging MapearAEntidad(
        DocumentoIngestado documento, ResultadoExtraccion extraccion, ResultadoValidacion validacion)
    {
        var f = extraccion.Factura!;
        var fechaIngesta = timeProvider.GetUtcNow().UtcDateTime;

        return new FacturaStaging
        {
            NumeroFactura = f.Factura.Numero ?? $"(SIN-NUMERO)-{documento.Id.ToString()[..8]}",
            FechaFactura = ParsearFecha(f.Factura.Fecha) ?? DateOnly.FromDateTime(fechaIngesta),
            FechaVencimiento = ParsearFecha(f.Factura.Vencimiento),
            Moneda = f.Factura.Moneda.Length == 3 ? f.Factura.Moneda : "EUR",
            BaseImponible = f.Totales.BaseImponible ?? 0m,
            CuotaIva = f.Totales.CuotaIva ?? 0m,
            RetencionIrpf = f.Totales.RetencionIrpf,
            Total = f.Totales.Total ?? 0m,
            ReverseCharge = f.Metadatos.ReverseCharge,
            Estado = validacion.Estado,
            JsonOriginalExtraido = extraccion.JsonOriginal!,
            NivelExtraccion = ExtractorAgent.NivelExtraccionGenerica,
            RutaPdfOriginal = documento.RutaPdf,
            FechaIngesta = fechaIngesta,
            Lineas = f.Lineas.Select((l, i) => new FacturaLinea
            {
                NumLinea = i + 1,
                Descripcion = l.Descripcion ?? "(sin descripción)",
                Cantidad = l.Cantidad ?? 0m,
                PrecioUnitario = l.PrecioUnitario ?? 0m,
                PorcentajeIva = l.PorcentajeIva ?? 0m,
                ImporteLinea = l.ImporteLinea ?? 0m,
            }).ToList(),
            Incidencias = validacion.Incidencias.Select(i => new ValidacionIncidencia
            {
                Codigo = i.Codigo,
                Detalle = i.Detalle,
                FechaCreacion = fechaIngesta,
            }).ToList(),
        };
    }

    private static DateOnly? ParsearFecha(string? iso) =>
        DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha
            : null;
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
