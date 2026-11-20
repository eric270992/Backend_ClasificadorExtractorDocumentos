using System.Diagnostics;

namespace ClassificadorExtractorDocumentos.Application;

/// <summary>
/// Font de traces per a les fases del pipeline que viuen a Application (ingesta, extracció, validació).
/// Mateix patró que LlmTracing i PdfTracing: la API la registra a OpenTelemetry
/// (AddSource(PipelineTracing.SourceName)) i els spans s'exporten a Phoenix niats sota el POST /documentos.
/// ActivitySource és del propi .NET (System.Diagnostics): no cal cap paquet NuGet a Application.
/// </summary>
public static class PipelineTracing
{
    public const string SourceName = "ClassificadorExtractorDocumentos.Pipeline";

    public static readonly ActivitySource Source = new(SourceName);
}
