namespace ClassificadorExtractorDocumentos.Domain.Contracts;

public sealed record DocumentoIngestado(Guid Id, string RutaPdf, IReadOnlyList<string> RutasImagenes);
