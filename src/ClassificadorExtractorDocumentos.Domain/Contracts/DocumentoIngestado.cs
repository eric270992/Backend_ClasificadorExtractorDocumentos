namespace ClassificadorExtractorDocumentos.Domain.Contracts;

/// <param name="TextoPdf">Texto embebido del PDF (null si no lo tiene: escaneado/fotografiado, o no
/// era un PDF). Referencia adicional para el Extractor, ver <see cref="IPdfTextExtractor"/>.</param>
public sealed record DocumentoIngestado(
    Guid Id, string RutaPdf, IReadOnlyList<string> RutasImagenes, string? TextoPdf = null);
