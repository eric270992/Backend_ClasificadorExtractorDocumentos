using System.Text.Json;
using ClassificadorExtractorDocumentos.Application.Llm;
using ClassificadorExtractorDocumentos.Application.Prompts;
using ClassificadorExtractorDocumentos.Domain.Contracts;

namespace ClassificadorExtractorDocumentos.Application.Consultor;

/// <summary>
/// Agente Consultor (SPEC §2.4): pregunta NL → SQL (LLM) → SQL-guard → ejecución → respuesta NL.
/// El guard corre SIEMPRE entre la generación y la BD; el LLM nunca toca la base de datos.
/// </summary>
public class ConsultorAgent(ILlmClient llmClient, IConsultaSqlEjecutor ejecutor, TimeProvider timeProvider)
{
    /// <summary>Filas que se pasan al LLM para redactar la respuesta (el resto se devuelve igualmente al cliente).</summary>
    private const int MaxFilasParaRedaccion = 30;

    private static readonly Lazy<(string Sistema, string Usuario)> Prompt =
        new(() => PromptLoader.Cargar("consultor-sql.md"));

    /// <summary>Errores de EJECUCIÓN (SQL Server rechaza la sintaxis) dan 1 reintento con feedback.
    /// Un rechazo del SQL-guard NO se reintenta jamás: es la postura de seguridad.</summary>
    private const int MaxIntentosGeneracion = 2;

    public async Task<RespuestaConsultor> PreguntarAsync(string pregunta, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pregunta))
        {
            return RespuestaConsultor.Rechazada("La pregunta está vacía.", null);
        }

        var (sistema, plantillaUsuario) = Prompt.Value;
        var hoy = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).ToString("yyyy-MM-dd");
        sistema = sistema.Replace("{{FECHA_HOY}}", hoy);
        var usuario = plantillaUsuario.Replace("{{PREGUNTA}}", pregunta.Trim());

        string? feedbackError = null;
        string? ultimoError = null;

        for (var intento = 1; intento <= MaxIntentosGeneracion; intento++)
        {
            // 1. Generación del SQL (con feedback del error anterior si lo hay)
            var usuarioIntento = feedbackError is null
                ? usuario
                : $"{usuario}\n\nATENCIÓN: tu consulta anterior falló al ejecutarse ({feedbackError}). " +
                  "Genera la consulta corregida. Devuelve SOLO el JSON.";

            var respuestaLlm = await llmClient.CompletarAsync(
                new LlmPeticion(sistema, usuarioIntento, ImagenesPng: [], ForzarJson: true), cancellationToken);

            var (sql, explicacion) = ParsearRespuesta(respuestaLlm);
            if (string.IsNullOrWhiteSpace(sql))
            {
                // El propio modelo declina (p. ej. le han pedido modificar datos)
                return RespuestaConsultor.Rechazada(explicacion ?? "No se pudo generar una consulta para esa pregunta.", null);
            }

            // 2. SQL-guard: validación por código antes de tocar la BD. Sin reintentos.
            var guard = SqlGuard.Validar(sql);
            if (!guard.EsSegura)
            {
                return RespuestaConsultor.Rechazada($"Consulta bloqueada por seguridad: {guard.Motivo}", sql);
            }

            // 3. Ejecución (solo SELECT, timeout corto)
            ResultadoConsultaSql datos;
            try
            {
                datos = await ejecutor.EjecutarSelectAsync(guard.SqlSeguro!, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                feedbackError = ex.Message;
                ultimoError = $"La consulta generada falló al ejecutarse: {ex.Message}";
                continue;
            }

            // 4. Redacción de la respuesta en lenguaje natural
            var respuestaNl = await RedactarRespuestaAsync(pregunta, guard.SqlSeguro!, datos, cancellationToken);

            return RespuestaConsultor.Ok(respuestaNl, explicacion, guard.SqlSeguro!, datos);
        }

        return RespuestaConsultor.Rechazada(ultimoError ?? "No se pudo ejecutar la consulta.", null);
    }

    private static (string? Sql, string? Explicacion) ParsearRespuesta(string respuestaLlm)
    {
        var json = LlmRespuesta.ExtraerPrimerObjetoJson(respuestaLlm);
        if (json is null)
        {
            return (null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var sql = doc.RootElement.TryGetProperty("sql", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString()
                : null;
            var explicacion = doc.RootElement.TryGetProperty("explicacion", out var e) && e.ValueKind == JsonValueKind.String
                ? e.GetString()
                : null;
            return (sql, explicacion);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private async Task<string> RedactarRespuestaAsync(
        string pregunta, string sql, ResultadoConsultaSql datos, CancellationToken cancellationToken)
    {
        var muestra = JsonSerializer.Serialize(datos.Filas.Take(MaxFilasParaRedaccion));

        var prompt =
            $"""
             El usuario preguntó: "{pregunta}"
             Se ejecutó esta consulta SQL: {sql}
             Resultado ({datos.Filas.Count} filas{(datos.Filas.Count > MaxFilasParaRedaccion ? $", se muestran {MaxFilasParaRedaccion}" : "")}): {muestra}

             Redacta una respuesta breve y clara a la pregunta, en el MISMO idioma de la pregunta,
             usando solo estos datos. Si no hay filas, dilo claramente. Sin markdown, texto plano.
             """;

        return await llmClient.CompletarAsync(
            new LlmPeticion(
                "Eres un asistente que responde preguntas sobre facturas a partir de datos ya consultados. Nunca inventes cifras.",
                prompt, ImagenesPng: [], ForzarJson: false),
            cancellationToken);
    }
}

public sealed record RespuestaConsultor(
    bool Exito,
    string? Respuesta,
    string? Explicacion,
    string? Sql,
    ResultadoConsultaSql? Datos,
    string? Motivo)
{
    public static RespuestaConsultor Ok(string respuesta, string? explicacion, string sql, ResultadoConsultaSql datos) =>
        new(true, respuesta, explicacion, sql, datos, null);

    public static RespuestaConsultor Rechazada(string motivo, string? sqlGenerado) =>
        new(false, null, null, sqlGenerado, null, motivo);
}
