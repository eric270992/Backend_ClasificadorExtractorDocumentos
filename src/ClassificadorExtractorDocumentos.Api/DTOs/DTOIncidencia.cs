namespace ClassificadorExtractorDocumentos.Api.DTOs;

/// <summary>Incidència de validació d'una factura. Es comparteix entre la pujada de documents i l'edició
/// de factures, perquè el frontend les pinta igual. Severidad és el nom de l'enum com a text.</summary>
public sealed record DTOIncidencia(string Codigo, string Detalle, string Severidad);
