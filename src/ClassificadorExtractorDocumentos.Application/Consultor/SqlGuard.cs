using System.Text.RegularExpressions;

namespace ClassificadorExtractorDocumentos.Application.Consultor;

/// <summary>
/// Validador por código del SQL generado por el LLM (SPEC §2.4): seguridad por diseño,
/// se aplica SIEMPRE antes de ejecutar. Solo SELECT de tablas en whitelist, una única
/// sentencia, sin comentarios, TOP 1000 forzado. Ante la duda, rechazar.
/// </summary>
public static partial class SqlGuard
{
    public const int MaxFilas = 1000;

    /// <summary>Tablas del staging consultables (whitelist §2.4).</summary>
    private static readonly HashSet<string> TablasPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "FacturasStaging", "FacturasLineas", "Proveedores", "ValidacionIncidencias",
    };

    /// <summary>Palabras que jamás deben aparecer, como palabra completa, en una consulta de lectura.</summary>
    private static readonly string[] PalabrasProhibidas =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE", "MERGE",
        "EXEC", "EXECUTE", "GRANT", "REVOKE", "INTO", "WAITFOR", "SHUTDOWN",
        "BACKUP", "RESTORE", "DBCC", "OPENROWSET", "OPENQUERY", "OPENDATASOURCE",
    ];

    [GeneratedRegex(@"(?:FROM|JOIN)\s+\[?(?<esquema>\w+)\]?\s*\.\s*\[?(?<tabla>\w+)\]?|(?:FROM|JOIN)\s+\[?(?<tabla>\w+)\]?", RegexOptions.IgnoreCase)]
    private static partial Regex TablasRegex();

    [GeneratedRegex(@"^\s*SELECT\s+(DISTINCT\s+)?TOP\b", RegexOptions.IgnoreCase)]
    private static partial Regex YaTieneTopRegex();

    [GeneratedRegex(@"^(\s*SELECT\s+)(DISTINCT\s+)?", RegexOptions.IgnoreCase)]
    private static partial Regex InicioSelectRegex();

    public static ResultadoGuard Validar(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return ResultadoGuard.Rechazar("La consulta está vacía.");
        }

        var limpio = sql.Trim();

        // Una única sentencia, sin comentarios ni cadenas de escape peligrosas
        if (limpio.Contains(';'))
        {
            return ResultadoGuard.Rechazar("No se permiten sentencias múltiples (';').");
        }
        if (limpio.Contains("--") || limpio.Contains("/*"))
        {
            return ResultadoGuard.Rechazar("No se permiten comentarios SQL.");
        }

        if (!limpio.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoGuard.Rechazar("Solo se permiten consultas SELECT.");
        }

        foreach (var palabra in PalabrasProhibidas)
        {
            if (Regex.IsMatch(limpio, $@"\b{palabra}\b", RegexOptions.IgnoreCase))
            {
                return ResultadoGuard.Rechazar($"Palabra prohibida en la consulta: {palabra}.");
            }
        }

        // Whitelist de tablas: toda tabla tras FROM/JOIN debe estar permitida
        var tablas = TablasRegex().Matches(limpio)
            .Select(m => m.Groups["tabla"].Value)
            .Where(t => t.Length > 0)
            .ToList();

        if (tablas.Count == 0)
        {
            return ResultadoGuard.Rechazar("La consulta no referencia ninguna tabla reconocible.");
        }

        var noPermitidas = tablas.Where(t => !TablasPermitidas.Contains(t)).Distinct().ToList();
        if (noPermitidas.Count > 0)
        {
            return ResultadoGuard.Rechazar($"Tablas fuera de la whitelist: {string.Join(", ", noPermitidas)}.");
        }

        // TOP 1000 forzado si la consulta no limita ya el resultado
        var sqlSeguro = YaTieneTopRegex().IsMatch(limpio)
            ? limpio
            : InicioSelectRegex().Replace(limpio, m => $"{m.Groups[1].Value}{m.Groups[2].Value}TOP {MaxFilas} ", 1);

        return ResultadoGuard.Aceptar(sqlSeguro);
    }
}

public sealed record ResultadoGuard(bool EsSegura, string? SqlSeguro, string? Motivo)
{
    public static ResultadoGuard Aceptar(string sql) => new(true, sql, null);
    public static ResultadoGuard Rechazar(string motivo) => new(false, null, motivo);
}
