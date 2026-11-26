namespace ClassificadorExtractorDocumentos.Api.DTOs;

/// <summary>Límits i opcions vigents del backend, perquè el frontend no els tingui escrits a mà.</summary>
public sealed record DTOConfiguracion(int MaxPaginasPorDocumento, long TamanoMaximoMb, bool GroundingHabilitado);
