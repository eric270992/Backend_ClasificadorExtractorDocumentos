using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.Extensions.Options;

namespace ClassificadorExtractorDocumentos.Infrastructure.Llm;

/// <summary>
/// Implementación de ILlmClient contra cualquier API compatible con el formato chat/completions
/// de OpenAI: Groq (cloud), LM Studio / Ollama (local)... El proveedor se elige por configuración
/// (Llm:BaseUrl + Llm:Model), sin tocar código — mitigación §7 del SPEC.
/// Las imágenes se envían como data-URI base64.
/// </summary>
public class OpenAiCompatibleLlmClient(HttpClient httpClient, IOptions<LlmOptions> options) : ILlmClient
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
        contenidoUsuario.AddRange(peticion.ImagenesPng.Select(png => (object)new
        {
            type = "image_url",
            image_url = new { url = $"data:image/png;base64,{Convert.ToBase64String(png)}" },
        }));

        var cuerpo = new
        {
            model = _options.Model,
            temperature = _options.Temperature,
            max_completion_tokens = _options.MaxTokens,
            response_format = peticion.ForzarJson && _options.UsarResponseFormatJson
                ? new { type = "json_object" }
                : null,
            messages = new object[]
            {
                new { role = "system", content = peticion.PromptSistema },
                new { role = "user", content = contenidoUsuario },
            },
        };

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
            throw new LlmException($"El proveedor LLM devolvió {(int)response.StatusCode}: {Truncar(detalle, 500)}");
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var contenido = json.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return contenido ?? throw new LlmException("El proveedor LLM devolvió una respuesta sin contenido.");
    }

    private static string Truncar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max] + "…";
}

public class LlmException(string message) : Exception(message);
