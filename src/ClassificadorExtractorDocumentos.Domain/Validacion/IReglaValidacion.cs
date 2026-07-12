using ClassificadorExtractorDocumentos.Domain.Contracts;

namespace ClassificadorExtractorDocumentos.Domain.Validacion;

/// <summary>Regla de negocio del Validador (SPEC §2.3), patrón Strategy. Las reglas son puras:
/// todo lo que necesitan del exterior (duplicados, fecha actual) llega precalculado en el contexto.</summary>
public interface IReglaValidacion
{
    string Codigo { get; }

    IEnumerable<Incidencia> Validar(ContextoValidacion contexto);
}

public enum SeveridadIncidencia
{
    Info,
    Revision,
    Rechazo,
}

public sealed record Incidencia(string Codigo, string Detalle, SeveridadIncidencia Severidad);

/// <param name="Factura">Extracción a validar.</param>
/// <param name="ExisteDuplicado">Precalculado por el orquestador: (proveedor + número) ya en staging.</param>
/// <param name="FechaReferencia">"Hoy" inyectado para que FECHA_RAZONABLE sea testeable.</param>
/// <param name="ToleranciaCuadre">±€ admitidos en los cuadres (appsettings; por defecto 0,02).</param>
public sealed record ContextoValidacion(
    FacturaExtraida Factura,
    bool ExisteDuplicado,
    DateOnly FechaReferencia,
    decimal ToleranciaCuadre = 0.02m);
