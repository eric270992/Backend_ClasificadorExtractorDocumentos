using ClassificadorExtractorDocumentos.Domain.Contracts;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ClassificadorExtractorDocumentos.Infrastructure.Consultor;

/// <summary>Ejecución de las consultas del Consultor con Dapper (SPEC §3: EF para CRUD,
/// Dapper para el Consultor). Timeout de 10 s (§2.4). Recibe SQL YA validado por el guard.</summary>
public class DapperConsultaSqlEjecutor(string connectionString) : IConsultaSqlEjecutor
{
    private const int TimeoutSegundos = 10;

    public async Task<ResultadoConsultaSql> EjecutarSelectAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var conexion = new SqlConnection(connectionString);

        var filas = await conexion.QueryAsync(
            new CommandDefinition(sql, commandTimeout: TimeoutSegundos, cancellationToken: cancellationToken));

        var lista = filas
            .Cast<IDictionary<string, object?>>()
            .Select(f => (IReadOnlyDictionary<string, object?>)f.ToDictionary(kv => kv.Key, kv => kv.Value))
            .ToList();

        var columnas = lista.Count > 0 ? lista[0].Keys.ToList() : [];

        return new ResultadoConsultaSql(columnas, lista);
    }
}
