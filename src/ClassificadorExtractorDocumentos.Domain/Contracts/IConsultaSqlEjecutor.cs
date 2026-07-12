namespace ClassificadorExtractorDocumentos.Domain.Contracts;

/// <summary>Ejecuta consultas SELECT ya validadas por el SQL-guard. Timeout corto (§2.4: 10 s).</summary>
public interface IConsultaSqlEjecutor
{
    Task<ResultadoConsultaSql> EjecutarSelectAsync(string sql, CancellationToken cancellationToken = default);
}

public sealed record ResultadoConsultaSql(
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Filas);
