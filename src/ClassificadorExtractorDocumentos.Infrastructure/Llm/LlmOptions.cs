namespace ClassificadorExtractorDocumentos.Infrastructure.Llm;

public class LlmOptions
{
    public const string SectionName = "Llm";

    public required string BaseUrl { get; set; }
    public required string Model { get; set; }

    /// <summary>Se inyecta desde user-secrets / variables de entorno. NUNCA en appsettings versionado (SPEC §6).
    /// Vacía para servidores locales sin autenticación (LM Studio, Ollama).</summary>
    public string? ApiKey { get; set; }

    public int MaxTokens { get; set; } = 8192;
    public double Temperature { get; set; } = 0.0;

    /// <summary>Enviar response_format json_object cuando la petición fuerza JSON. Groq lo soporta;
    /// LM Studio no (solo json_schema/text) → ponerlo a false y confiar en el prompt + parser tolerante.</summary>
    public bool UsarResponseFormatJson { get; set; } = true;
}
