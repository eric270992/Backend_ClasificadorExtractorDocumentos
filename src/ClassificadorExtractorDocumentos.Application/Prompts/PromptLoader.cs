using System.Reflection;

namespace ClassificadorExtractorDocumentos.Application.Prompts;

/// <summary>Carga prompts versionados embebidos en el ensamblado y los separa en
/// (sistema, usuario) por el marcador ===USER===, descartando el comentario de cabecera.</summary>
public static class PromptLoader
{
    private const string SeparadorUsuario = "===USER===";

    public static (string Sistema, string Usuario) Cargar(string nombreFichero)
    {
        var recurso = $"ClassificadorExtractorDocumentos.Application.Prompts.{nombreFichero}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException($"No se encontró el prompt embebido '{recurso}'.");
        using var reader = new StreamReader(stream);
        var contenido = reader.ReadToEnd();

        var indice = contenido.IndexOf(SeparadorUsuario, StringComparison.Ordinal);
        if (indice < 0)
        {
            throw new InvalidOperationException($"El prompt '{nombreFichero}' no contiene el separador '{SeparadorUsuario}'.");
        }

        var sistema = contenido[..indice].Trim();
        if (sistema.StartsWith("<!--", StringComparison.Ordinal))
        {
            var finComentario = sistema.IndexOf("-->", StringComparison.Ordinal);
            if (finComentario > 0)
            {
                sistema = sistema[(finComentario + 3)..].Trim();
            }
        }

        var usuario = contenido[(indice + SeparadorUsuario.Length)..].Trim();
        return (sistema, usuario);
    }
}
