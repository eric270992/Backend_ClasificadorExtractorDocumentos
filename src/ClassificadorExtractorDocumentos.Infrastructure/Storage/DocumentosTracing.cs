using System.Diagnostics;

namespace ClassificadorExtractorDocumentos.Infrastructure.Storage;

/// <summary>
/// Font de traces per al tractament de fitxers del document: normalitzar imatges (fotos) i
/// llegir/escriure les pàgines a disc. A Phoenix hi havia ~2 s sense cobrir abans de la crida
/// al model quan es pujava una foto; aquests spans diuen d'on surten.
/// </summary>
public static class DocumentosTracing
{
    public const string SourceName = "ClassificadorExtractorDocumentos.Documentos";

    public static readonly ActivitySource Source = new(SourceName);
}
