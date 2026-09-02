namespace ClassificadorExtractorDocumentos.Application.Ingesta;

/// <summary>Opciones de la ingesta, sección "Ingesta" de la configuración.</summary>
public class IngestaOptions
{
    public const string SectionName = "Ingesta";

    /// <summary>
    /// Si es true (por defecto), se extrae el texto embebido del PDF (PdfPig) y se pasa al Extractor
    /// como referencia junto a las imágenes. Ponerlo a false vuelve al modo solo-visión anterior:
    /// prompt más pequeño y sin la fase de extracción de texto — útil para medir su impacto en la latencia.
    /// </summary>
    public bool UsarTextoPdf { get; set; } = true;
}
