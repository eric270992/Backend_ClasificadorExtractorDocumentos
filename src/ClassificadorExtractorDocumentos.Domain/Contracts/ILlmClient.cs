namespace ClassificadorExtractorDocumentos.Domain.Contracts;

/// <summary>
/// Única puerta de entrada al LLM (SPEC §7: mitigación del riesgo "cambio de proveedor").
/// Cualquier agente que necesite el modelo pasa por aquí.
/// </summary>
public interface ILlmClient
{
    /// <summary>Envía un prompt con imágenes opcionales y devuelve el texto de respuesta del modelo.</summary>
    Task<string> CompletarAsync(LlmPeticion peticion, CancellationToken cancellationToken = default);
}

/// <param name="PromptSistema">Instrucciones de sistema (rol, formato de salida...).</param>
/// <param name="PromptUsuario">Texto del turno de usuario.</param>
/// <param name="ImagenesPng">Imágenes PNG adjuntas al turno de usuario (páginas del documento), en orden.</param>
/// <param name="ForzarJson">Pedir al proveedor salida en modo JSON si lo soporta.</param>
public sealed record LlmPeticion(
    string PromptSistema,
    string PromptUsuario,
    IReadOnlyList<byte[]> ImagenesPng,
    bool ForzarJson = true);
