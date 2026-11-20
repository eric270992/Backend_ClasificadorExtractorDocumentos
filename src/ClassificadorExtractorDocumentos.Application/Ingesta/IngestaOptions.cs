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

    /// <summary>
    /// Màxim de pàgines que s'accepten per document. Un PDF amb més pàgines es rebutja abans de fer cap feina
    /// (ni renderitzar ni cridar el model) i l'usuari rep un missatge clar. És preferible a retallar pàgines
    /// sense avisar: en una factura llarga els totals solen ser a l'última pàgina.
    /// El frontend el demana a GET /configuracion per mostrar-lo a l'usuari.
    /// Ha de ser coherent amb el límit d'imatges del proveïdor LLM (Llm:Perfiles:*:MaxImagenesPorPeticion).
    /// </summary>
    public int MaxPaginasPorDocumento { get; set; } = 4;
}
