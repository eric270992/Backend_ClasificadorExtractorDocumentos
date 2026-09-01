using System.Diagnostics;

namespace ClassificadorExtractorDocumentos.Infrastructure.Llm;

/// <summary>
/// Fuente de trazas para las llamadas al LLM. La API la registra en OpenTelemetry
/// (AddSource(LlmTracing.SourceName)) y las exporta a Phoenix vía OTLP. Los spans usan
/// las convenciones semánticas de OpenInference (openinference.span.kind = "LLM", etc.)
/// para que Phoenix los muestre como llamadas LLM con prompt, respuesta y tokens.
/// </summary>
public static class LlmTracing
{
    public const string SourceName = "ClassificadorExtractorDocumentos.Llm";

    public static readonly ActivitySource Source = new(SourceName);
}
