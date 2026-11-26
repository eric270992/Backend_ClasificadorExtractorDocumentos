using ClassificadorExtractorDocumentos.Domain.Contracts;

namespace ClassificadorExtractorDocumentos.Api.DTOs;

/// <summary>201 de la pujada amb procesar=false: només s'ha fet la ingesta (document → imatges).</summary>
public sealed record DTODocumentoIngestado(Guid DocumentoId, int Paginas);

/// <summary>201 de la pujada amb procesar=true: el pipeline complet ha desat la factura. Estado és el
/// nom de l'enum com a text.</summary>
public sealed record DTOFacturaProcesada(
    Guid DocumentoId,
    int? FacturaId,
    string Estado,
    IEnumerable<DTOIncidencia> Incidencias);

/// <summary>422 de la pujada quan el pipeline no ha pogut extreure la factura.</summary>
public sealed record DTOErrorProcesado(Guid DocumentoId, string? Error);

/// <summary>422 de la pujada quan el document té més pàgines de les permeses. Porta les dues xifres
/// perquè el frontend actualitzi el límit si ha canviat.</summary>
public sealed record DTODocumentoDemasiadoLargo(string Error, int Paginas, int MaxPaginas);

/// <summary>200 de l'extracció de depuració (no es desa res).</summary>
public sealed record DTOExtraccion(FacturaExtraida? Factura, int Reintentos);

/// <summary>422 de l'extracció de depuració.</summary>
public sealed record DTOErrorExtraccion(string? Error, int Reintentos);
