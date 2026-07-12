using System.Text.RegularExpressions;

namespace ClassificadorExtractorDocumentos.Domain.ValueObjects;

/// <summary>
/// Concepto de dominio NIF/CIF/VAT en un único lugar: normalización (contrato §2.1: sin
/// guiones/espacios, mayúsculas) y validación de formato (ES + básico UE). Quien normaliza
/// (parser de extracción) y quien valida (regla NIF_FORMATO) delegan aquí.
/// </summary>
public static partial class Nif
{
    // DNI: 8 dígitos + letra · NIE: X/Y/Z + 7 dígitos + letra · CIF: letra + 7 dígitos + dígito/letra
    [GeneratedRegex("^([0-9]{8}[A-Z]|[XYZ][0-9]{7}[A-Z]|[ABCDEFGHJKLMNPQRSUVW][0-9]{7}[0-9A-J])$")]
    private static partial Regex EspanolRegex();

    // VAT UE genérico: código de país + 2-13 alfanuméricos con al menos un dígito
    // (validación básica, no por país; evita falsos positivos con palabras como "HOLA")
    [GeneratedRegex(@"^[A-Z]{2}(?=[0-9A-Z+*.]{2,13}$)[A-Z+*.]*[0-9][0-9A-Z+*.]*$")]
    private static partial Regex VatUeRegex();

    /// <summary>"B-12.345.678 " → "B12345678". Null o blanco → null (nunca inventar).</summary>
    public static string? Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var limpio = texto.Replace("-", "").Replace(".", "").Replace(" ", "").ToUpperInvariant();
        return limpio.Length == 0 ? null : limpio;
    }

    /// <summary>¿Formato válido (español o VAT UE básico)? Se asume ya normalizado.</summary>
    public static bool FormatoValido(string nif) =>
        EspanolRegex().IsMatch(nif) || VatUeRegex().IsMatch(nif);
}
