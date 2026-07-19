using System.Globalization;
using System.Text.RegularExpressions;

namespace ClassificadorExtractorDocumentos.Application.Llm;

/// <summary>Utilidades para tratar respuestas de LLM: los modelos a veces envuelven el JSON
/// en fences de markdown o prosa; aquí se localiza el primer objeto JSON balanceado.</summary>
public static partial class LlmRespuesta
{
    public static string? ExtraerPrimerObjetoJson(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var inicio = texto.IndexOf('{');
        if (inicio < 0)
        {
            return null;
        }

        var profundidad = 0;
        var enString = false;
        var escapado = false;
        for (var i = inicio; i < texto.Length; i++)
        {
            var c = texto[i];
            if (escapado) { escapado = false; continue; }
            if (c == '\\' && enString) { escapado = true; continue; }
            if (c == '"') { enString = !enString; continue; }
            if (enString) { continue; }
            if (c == '{') { profundidad++; }
            else if (c == '}')
            {
                profundidad--;
                if (profundidad == 0)
                {
                    return texto[inicio..(i + 1)];
                }
            }
        }

        return null;
    }

    /// <summary>Modelos pequeños a veces "muestran el cálculo" en vez de darlo hecho, p. ej.
    /// "baseImponible": 255.00 + 189.90 + 190.00 — sintácticamente inválido en JSON. Se detecta
    /// (solo tras ':' y antes de ',' '}' ']', nunca dentro de una cadena) y se resuelve la suma
    /// antes de parsear, en vez de fallar la extracción entera por un solo campo.</summary>
    public static string ResolverSumasSinEvaluar(string json) =>
        SumaEnLinea().Replace(json, m =>
        {
            var suma = m.Groups["expr"].Value
                .Split('+', StringSplitOptions.TrimEntries)
                .Sum(n => decimal.Parse(n, CultureInfo.InvariantCulture));
            return $": {suma.ToString(CultureInfo.InvariantCulture)}";
        });

    [GeneratedRegex(@":\s*(?<expr>-?\d+(?:\.\d+)?(?:\s*\+\s*-?\d+(?:\.\d+)?)+)\s*(?=[,}\]])")]
    private static partial Regex SumaEnLinea();
}
