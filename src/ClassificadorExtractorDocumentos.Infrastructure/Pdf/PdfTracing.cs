using System.Diagnostics;

namespace ClassificadorExtractorDocumentos.Infrastructure.Pdf;

/// <summary>
/// Fuente de trazas para las fases de PDF de la ingesta (conversión a imágenes y extracción de
/// texto). La API la registra en OpenTelemetry (AddSource(PdfTracing.SourceName)) para que estos
/// spans aniden bajo el POST /documentos y desglosen dónde se va el tiempo: PDF vs LLM vs BD.
/// </summary>
public static class PdfTracing
{
    public const string SourceName = "ClassificadorExtractorDocumentos.Pdf";

    public static readonly ActivitySource Source = new(SourceName);
}
