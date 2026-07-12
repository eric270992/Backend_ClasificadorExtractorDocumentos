using System.Globalization;

namespace ClassificadorExtractorDocumentos.Domain.Parsers;

/// <summary>
/// Normaliza importes tal como aparecen en documentos ("1.234,56 €", "1,234.56", "1234,56")
/// a decimal. Regla del contrato §2.1: números SIEMPRE con punto decimal en el JSON.
/// </summary>
public static class NumeroParser
{
    /// <summary>Intenta normalizar un importe. Devuelve null si el texto no es un número reconocible (nunca inventar).</summary>
    public static decimal? Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        // Quitar símbolos de moneda, espacios (incl. no-break) y signos sueltos comunes
        var limpio = texto.Trim()
            .Replace("€", "").Replace("$", "").Replace("£", "")
            .Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
            .Replace("USD", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "").Replace(" ", "");

        if (limpio.Length == 0)
        {
            return null;
        }

        var ultimaComa = limpio.LastIndexOf(',');
        var ultimoPunto = limpio.LastIndexOf('.');

        string normalizado;
        if (ultimaComa >= 0 && ultimoPunto >= 0)
        {
            // Ambos separadores presentes: el que aparece más a la derecha es el decimal
            normalizado = ultimaComa > ultimoPunto
                ? limpio.Replace(".", "").Replace(',', '.')   // formato ES: 1.234,56
                : limpio.Replace(",", "");                     // formato EN: 1,234.56
        }
        else if (ultimaComa >= 0)
        {
            // Solo coma: decimal si va seguida de 1-2 dígitos ("1234,56"); si no, separador de miles ("1,234")
            var digitosTrasComa = limpio.Length - ultimaComa - 1;
            normalizado = digitosTrasComa is 1 or 2
                ? limpio.Replace(",", ".")
                : limpio.Replace(",", "");
        }
        else if (ultimoPunto >= 0)
        {
            // Solo punto: decimal si va seguido de 1-2 dígitos ("1234.56"); si no, separador de miles ("1.234")
            var digitosTrasPunto = limpio.Length - ultimoPunto - 1;
            normalizado = digitosTrasPunto is 1 or 2
                ? limpio
                : limpio.Replace(".", "");
        }
        else
        {
            normalizado = limpio;
        }

        return decimal.TryParse(normalizado, NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;
    }
}
