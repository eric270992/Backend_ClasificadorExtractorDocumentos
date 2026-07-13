namespace ClassificadorExtractorDocumentos.Application.Ingesta;

/// <summary>Formatos de entrada admitidos por la ingesta.</summary>
public enum FormatoDocumento
{
    Pdf,
    Jpeg,
    Png,
    Desconocido,
}

/// <summary>
/// Detecta el formato de un documento por sus "magic bytes" (no por extensión ni Content-Type, que
/// se pueden falsear o faltar). Es el punto único que decide el camino de ingesta.
/// </summary>
public static class DeteccionFormato
{
    public static FormatoDocumento Detectar(ReadOnlySpan<byte> bytes)
    {
        // PDF: "%PDF"
        if (bytes.Length >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
        {
            return FormatoDocumento.Pdf;
        }
        // JPEG: FF D8 FF
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return FormatoDocumento.Jpeg;
        }
        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return FormatoDocumento.Png;
        }
        return FormatoDocumento.Desconocido;
    }

    public static bool EsImagen(this FormatoDocumento formato) =>
        formato is FormatoDocumento.Jpeg or FormatoDocumento.Png;

    public static string Extension(this FormatoDocumento formato) => formato switch
    {
        FormatoDocumento.Pdf => "pdf",
        FormatoDocumento.Jpeg => "jpg",
        FormatoDocumento.Png => "png",
        _ => "bin",
    };
}

/// <summary>El documento subido no está en un formato admitido (PDF, JPEG o PNG).</summary>
public class FormatoNoSoportadoException()
    : Exception("Formato no admitido. Solo se aceptan PDF, JPEG o PNG.");
