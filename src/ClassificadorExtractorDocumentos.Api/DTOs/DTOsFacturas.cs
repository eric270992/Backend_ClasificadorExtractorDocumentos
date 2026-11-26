namespace ClassificadorExtractorDocumentos.Api.DTOs;

/// <summary>200 de l'edició manual: estat i incidències després de tornar a validar. Estado és el nom de
/// l'enum com a text.</summary>
public sealed record DTOEdicionFactura(
    string Estado,
    bool HuboCambios,
    IEnumerable<DTOIncidencia> Incidencias);
