using System.Diagnostics;

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence;

/// <summary>
/// Font de traces per a l'accés a dades (consulta de duplicat i desat de la factura). Es mesura a nivell
/// d'operació del repositori, no de cada SQL: per veure les consultes una a una caldria el paquet
/// OpenTelemetry.Instrumentation.EntityFrameworkCore, que de moment no s'ha afegit.
/// </summary>
public static class PersistenciaTracing
{
    public const string SourceName = "ClassificadorExtractorDocumentos.Persistencia";

    public static readonly ActivitySource Source = new(SourceName);
}
