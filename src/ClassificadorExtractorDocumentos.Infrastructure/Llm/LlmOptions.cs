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

    /// <summary>Format en què s'envien les imatges al model: "jpeg" (per defecte) o "png".
    /// Les pàgines es continuen desant en PNG a disc (original sense pèrdua); només es recodifiquen
    /// per al transport. JPEG ocupa 5-10 vegades menys → menys temps de pujada al servidor.
    /// Si algun proveïdor llegís pitjor les imatges, es pot tornar a "png" per perfil sense tocar codi.</summary>
    public string FormatoImagen { get; set; } = "jpeg";

    /// <summary>Qualitat JPEG (1-100). 90 manté el text petit i els decimals nets; per sota de ~80
    /// comencen a aparèixer artefactes al voltant dels caràcters, que és el que volem evitar.</summary>
    public int CalidadJpeg { get; set; } = 90;

    /// <summary>Desactiva el "thinking" (raonament previ) dels models híbrids servits amb vLLM (Qwen3/3.5/3.6...).
    /// S'envia com a <c>chat_template_kwargs.enable_thinking=false</c>: és la plantilla de xat del model qui llegeix
    /// aquesta variable i deixa d'obrir el bloc de raonament. Per a una extracció a JSON el raonament no aporta
    /// precisió apreciable però multiplica els tokens generats (i el temps). Per defecte false: els proveïdors que
    /// no són vLLM (Groq, Nvidia...) podrien rebutjar un camp que no coneixen, així que s'activa només per perfil.</summary>
    public bool DesactivarThinking { get; set; } = false;

    /// <summary>Màxim d'imatges que el servidor accepta en una sola petició (null = sense límit propi).
    /// És una restricció del desplegament, no del pipeline: p. ej. el vLLM d'EIO està arrencat amb un
    /// límit de 4 imatges per prompt i retorna 400 si n'hi arriben més. Per això viu al perfil.</summary>
    public int? MaxImagenesPorPeticion { get; set; }
}
