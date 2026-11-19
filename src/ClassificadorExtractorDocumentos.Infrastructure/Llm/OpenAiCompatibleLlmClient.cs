using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClassificadorExtractorDocumentos.Infrastructure.Llm;

/// <summary>
/// Implementación de ILlmClient contra cualquier API compatible con el formato chat/completions
/// de OpenAI: Groq (cloud), LM Studio / Ollama (local)... El proveedor se elige por configuración
/// (Llm:BaseUrl + Llm:Model), sin tocar código — mitigación §7 del SPEC.
/// Las imágenes se envían como data-URI base64.
/// </summary>
public class OpenAiCompatibleLlmClient(
    HttpClient httpClient,
    IOptions<LlmOptions> options,
    ILogger<OpenAiCompatibleLlmClient> logger) : ILlmClient
{
    private readonly LlmOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<string> CompletarAsync(LlmPeticion peticion, CancellationToken cancellationToken = default)
    {
        var contenidoUsuario = new List<object>
        {
            new { type = "text", text = peticion.PromptUsuario },
        };
        // Límit d'imatges del servidor (per perfil). Es queden les primeres pàgines, que és on hi ha
        // les dades de la factura (capçalera, emissor, totals). Es registra al log i a la traça perquè el
        // retall no passi desapercebut: millor una extracció amb 4 pàgines que un error 400.
        var imagenes = peticion.ImagenesPng;
        if (_options.MaxImagenesPorPeticion is int maxImagenes && imagenes.Count > maxImagenes)
        {
            logger.LogWarning(
                "El document té {Total} pàgines i el proveïdor n'admet {Max} per petició: s'envien les {Max} primeres.",
                imagenes.Count, maxImagenes, maxImagenes);
            imagenes = imagenes.Take(maxImagenes).ToList();
        }

        // Les imatges arriben sempre en PNG (pàgines de PDF o fotos normalitzades). Abans d'enviar-les
        // es recodifiquen segons el perfil (JPEG per defecte) per reduir la mida de la petició.
        var imagenesEnviadas = imagenes
            .Select(png => CodificadorImagenLlm.Preparar(png, _options.FormatoImagen, _options.CalidadJpeg))
            .ToList();
        contenidoUsuario.AddRange(imagenesEnviadas.Select(img => (object)new
        {
            type = "image_url",
            image_url = new { url = $"data:{img.MimeType};base64,{Convert.ToBase64String(img.Bytes)}" },
        }));

        var cuerpo = new
        {
            model = _options.Model,
            temperature = _options.Temperature,
            max_completion_tokens = _options.MaxTokens,
            response_format = peticion.ForzarJson && _options.UsarResponseFormatJson
                ? new { type = "json_object" }
                : null,
            // Només s'envia si el perfil ho demana. Quan és null, el serialitzador l'omet
            // (DefaultIgnoreCondition = WhenWritingNull) i la petició queda exactament com abans.
            chat_template_kwargs = _options.DesactivarThinking
                ? new { enable_thinking = false }
                : null,
            messages = new object[]
            {
                new { role = "system", content = peticion.PromptSistema },
                new { role = "user", content = contenidoUsuario },
            },
        };

        // Span OpenInference para Phoenix: envuelve toda la llamada y expone prompt/respuesta/tokens.
        using var activity = LlmTracing.Source.StartActivity("llm.chat", ActivityKind.Client);
        activity?.SetTag("openinference.span.kind", "LLM");
        activity?.SetTag("llm.system", _options.BaseUrl);
        activity?.SetTag("llm.model_name", _options.Model);
        activity?.SetTag("llm.thinking_desactivado", _options.DesactivarThinking);
        activity?.SetTag("llm.invocation_parameters",
            JsonSerializer.Serialize(new { _options.Temperature, max_tokens = _options.MaxTokens }, JsonOptions));
        activity?.SetTag("input.mime_type", "text/plain");
        activity?.SetTag("input.value", peticion.PromptUsuario);
        activity?.SetTag("llm.prompts.0.role", "system");
        activity?.SetTag("llm.prompts.0.content", peticion.PromptSistema);
        activity?.SetTag("llm.input_messages.0.message.role", "user");
        activity?.SetTag("llm.input_messages.0.message.content", peticion.PromptUsuario);
        activity?.SetTag("llm.input_images.count", imagenesEnviadas.Count);
        activity?.SetTag("llm.input_images.descartadas", peticion.ImagenesPng.Count - imagenesEnviadas.Count);
        // Bytes reals enviats (després de recodificar): permet veure a Phoenix l'efecte del JPEG
        activity?.SetTag("llm.input_images.bytes", imagenesEnviadas.Sum(i => (long)i.Bytes.Length));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(cuerpo, options: JsonOptions),
        };

        // JsonContent.Create añade "; charset=utf-8" por defecto; algunos proveedores (Nvidia NIM)
        // lo rechazan con 415 exigiendo el media type exacto "application/json", sin parámetros.
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        // Los servidores locales (LM Studio, Ollama) no requieren clave
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new("Bearer", _options.ApiKey);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detalle = await response.Content.ReadAsStringAsync(cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Error, $"HTTP {(int)response.StatusCode}");
            throw new LlmException($"El proveedor LLM devolvió {(int)response.StatusCode}: {Truncar(detalle, 500)}");
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = json.RootElement;
        var choice = root.GetProperty("choices")[0];
        var mensaje = choice.GetProperty("message");
        var contenido = mensaje.GetProperty("content").GetString();

        // vLLM amb "reasoning parser" retorna el raonament en un camp separat que no fem servir.
        // Es registra la seva mida a la traça: si el thinking continua actiu (p. ex. el flag no ha arribat
        // al model), es veurà a Phoenix en lloc de quedar amagat dins el temps de resposta.
        if (mensaje.TryGetProperty("reasoning_content", out var razonamiento)
            && razonamiento.ValueKind == JsonValueKind.String)
        {
            activity?.SetTag("llm.reasoning.length", razonamiento.GetString()!.Length);
        }

        // El bloque usage (opcional según proveedor) alimenta las métricas de tokens de Phoenix.
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var pt))
                activity?.SetTag("llm.token_count.prompt", pt.GetInt32());
            if (usage.TryGetProperty("completion_tokens", out var ct))
                activity?.SetTag("llm.token_count.completion", ct.GetInt32());
            if (usage.TryGetProperty("total_tokens", out var tt))
                activity?.SetTag("llm.token_count.total", tt.GetInt32());
        }

        if (!string.IsNullOrEmpty(contenido))
        {
            activity?.SetTag("output.mime_type", "text/plain");
            activity?.SetTag("output.value", contenido);
            activity?.SetTag("llm.output_messages.0.message.role", "assistant");
            activity?.SetTag("llm.output_messages.0.message.content", contenido);
            return contenido;
        }

        // content null/vacío: exponer finish_reason para diagnosticar. La causa habitual con
        // facturas complejas es finish_reason="length" (la respuesta se truncó al agotar
        // max_completion_tokens) → hay que subir Llm:MaxTokens.
        var finishReason = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null;
        var pista = finishReason switch
        {
            "length" => $" El motivo fue 'length': la respuesta se truncó al alcanzar el límite de {_options.MaxTokens} tokens. Aumenta Llm:MaxTokens.",
            "content_filter" => " El motivo fue 'content_filter': el proveedor bloqueó la respuesta.",
            _ => string.Empty,
        };
        activity?.SetTag("llm.finish_reason", finishReason);
        activity?.SetStatus(ActivityStatusCode.Error, $"sin contenido (finish_reason={finishReason})");
        throw new LlmException(
            $"El proveedor LLM devolvió una respuesta sin contenido (finish_reason={finishReason ?? "desconocido"}).{pista}");
    }

    private static string Truncar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max] + "…";
}

public class LlmException(string message) : Exception(message);
