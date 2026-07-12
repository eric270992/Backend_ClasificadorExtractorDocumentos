using System.Globalization;
using System.Text.RegularExpressions;

namespace ClassificadorExtractorDocumentos.Domain.Parsers;

/// <summary>
/// Normaliza fechas tal como aparecen en documentos a ISO 8601 (yyyy-MM-dd), regla del contrato §2.1:
/// "5 julio 2026" → "2026-07-05". Devuelve null si no es reconocible (nunca inventar).
/// </summary>
public static partial class FechaParser
{
    private static readonly string[] FormatosExplicitos =
    [
        "yyyy-MM-dd", "yyyy/MM/dd",
        "dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy",
        "d/M/yyyy", "d-M-yyyy", "d.M.yyyy",
        "dd/MM/yy", "d/M/yy",
    ];

    private static readonly Dictionary<string, int> Meses = new(StringComparer.OrdinalIgnoreCase)
    {
        // Español
        ["enero"] = 1, ["febrero"] = 2, ["marzo"] = 3, ["abril"] = 4, ["mayo"] = 5, ["junio"] = 6,
        ["julio"] = 7, ["agosto"] = 8, ["septiembre"] = 9, ["octubre"] = 10, ["noviembre"] = 11, ["diciembre"] = 12,
        // Catalán ("abril" y "octubre" coinciden con el español)
        ["gener"] = 1, ["febrer"] = 2, ["març"] = 3, ["maig"] = 5, ["juny"] = 6,
        ["juliol"] = 7, ["agost"] = 8, ["setembre"] = 9, ["novembre"] = 11, ["desembre"] = 12,
        // Inglés
        ["january"] = 1, ["february"] = 2, ["march"] = 3, ["april"] = 4, ["may"] = 5, ["june"] = 6,
        ["july"] = 7, ["august"] = 8, ["september"] = 9, ["october"] = 10, ["november"] = 11, ["december"] = 12,
        ["jan"] = 1, ["feb"] = 2, ["mar"] = 3, ["apr"] = 4, ["jun"] = 6,
        ["jul"] = 7, ["aug"] = 8, ["sep"] = 9, ["oct"] = 10, ["nov"] = 11, ["dec"] = 12,
    };

    [GeneratedRegex(@"(\d{1,2})\s*(?:de\s+|d')?([\p{L}]+)\s*(?:de\s+|del\s+|,\s*)?(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex FechaTextualRegex();

    [GeneratedRegex(@"([\p{L}]+)\s+(\d{1,2})\s*,\s*(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex FechaTextualInglesaRegex();

    public static string? Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var limpio = texto.Trim();

        if (DateOnly.TryParseExact(limpio, FormatosExplicitos, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var fecha))
        {
            return fecha.ToString("yyyy-MM-dd");
        }

        // "5 julio 2026", "5 de julio de 2026", "17 juliol 2026"
        var m = FechaTextualRegex().Match(limpio);
        if (m.Success && TryMes(m.Groups[2].Value, out var mes) &&
            TryCrear(int.Parse(m.Groups[3].Value), mes, int.Parse(m.Groups[1].Value), out var f1))
        {
            return f1;
        }

        // "July 5, 2026"
        var mi = FechaTextualInglesaRegex().Match(limpio);
        if (mi.Success && TryMes(mi.Groups[1].Value, out var mesEn) &&
            TryCrear(int.Parse(mi.Groups[3].Value), mesEn, int.Parse(mi.Groups[2].Value), out var f2))
        {
            return f2;
        }

        return null;
    }

    private static bool TryMes(string nombre, out int mes) => Meses.TryGetValue(nombre, out mes) && mes >= 1 && mes <= 12;

    private static bool TryCrear(int anio, int mes, int dia, out string? iso)
    {
        try
        {
            iso = new DateOnly(anio, mes, dia).ToString("yyyy-MM-dd");
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            iso = null;
            return false;
        }
    }
}
