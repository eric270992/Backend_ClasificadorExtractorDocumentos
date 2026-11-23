using System.Text.Json;
using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Application.Llm;
using ClassificadorExtractorDocumentos.Application.Prompts;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Grounding;
using Microsoft.Extensions.Logging;

namespace ClassificadorExtractorDocumentos.Application.Grounding;

/// <summary>
/// Grounding amb el MODEL DE VISIÓ: se li passen les imatges i els valors ja extrets i se li demana la caixa
/// (bbox_2d) de cada un. Funciona també amb fotos i escanejats (on no hi ha capa de text), però:
/// - les caixes són aproximades (el model "dibuixa" a ull, sobre una imatge redimensionada);
/// - és una crida al model sencera de més, amb el cost de temps que ja coneixem del servidor d'EIO.
/// Per això és un mètode alternatiu, per comparar-lo amb l'híbrid, i no s'executa durant la ingesta.
/// <para>
/// Es fa una crida APART de l'extracció (i no es demanen les caixes dins el mateix JSON de l'extracció) per
/// no tocar el prompt que ja funciona: afegir-li coordenades podria empitjorar la qualitat dels valors.
/// </para>
/// </summary>
public class GroundingLlmAgent(ILlmClient llmClient, ILogger<GroundingLlmAgent> logger)
{
    /// <summary>Escala de les coordenades que demanem al model (0-1000, format natiu de Qwen3-VL).</summary>
    private const double EscalaCoordenadas = 1000.0;

    private static readonly Lazy<(string Sistema, string Usuario)> Prompt =
        new(() => PromptLoader.Cargar("grounding.md"));

    public async Task<IReadOnlyList<CampoLocalizado>> LocalizarAsync(
        IReadOnlyList<byte[]> paginasPng, IReadOnlyList<CampoBuscado> campos, CancellationToken cancellationToken = default)
    {
        using var activity = PipelineTracing.Source.StartActivity("grounding.llm");
        activity?.SetTag("grounding.campos", campos.Count);

        // Mateix límit de pàgines que l'extracció: el proveïdor no n'accepta més per petició
        var paginasEnviadas = paginasPng.Take(ExtractorAgent.MaxPaginasPorPeticion).ToList();

        var (sistema, usuarioBase) = Prompt.Value;

        // La llista de camps va en JSON perquè el model la llegeixi sense ambigüitats (una descripció de línia
        // pot contenir comes, dos punts...). Només el que necessita: clau, etiqueta i valor.
        var listaCampos = JsonSerializer.Serialize(campos.Select(campo => new
        {
            campo = campo.Campo,
            etiqueta = campo.Etiqueta,
            valor = campo.ValorMostrado,
        }));
        var usuario = $"{usuarioBase}\n{listaCampos}";

        var respuesta = await llmClient.CompletarAsync(new LlmPeticion(sistema, usuario, paginasEnviadas), cancellationToken);

        var cajasPorCampo = LeerRespuesta(respuesta, paginasEnviadas.Count);
        activity?.SetTag("grounding.localizados", cajasPorCampo.Count);

        // Es retornen TOTS els camps demanats, en el mateix ordre: els que el model no ha trobat (o ha retornat
        // malament) surten sense caixa, igual que fa el mètode híbrid. Així el frontend els tracta igual.
        return campos.Select(campo => cajasPorCampo.TryGetValue(campo.Campo, out var localizacion)
                ? new CampoLocalizado(campo.Campo, campo.Etiqueta, campo.ValorMostrado,
                    localizacion.Pagina, localizacion.Caja, Puntuacion: null, NumCandidatos: null)
                : new CampoLocalizado(campo.Campo, campo.Etiqueta, campo.ValorMostrado,
                    Pagina: null, Caja: null, Puntuacion: null, NumCandidatos: null))
            .ToList();
    }

    /// <summary>
    /// Llegeix {"campos":[{"campo":..., "pagina":..., "bbox_2d":[x1,y1,x2,y2]}]} de manera tolerant: un element mal
    /// format es descarta (aquell camp queda sense caixa) en lloc de fer fallar tota la resposta. Aquí no hi ha
    /// reintent com a l'extracció: per a una prova visual no val la pena pagar una segona crida.
    /// </summary>
    private Dictionary<string, (int Pagina, CajaNormalizada Caja)> LeerRespuesta(string respuesta, int numPaginas)
    {
        var resultado = new Dictionary<string, (int Pagina, CajaNormalizada Caja)>();

        // Reutilitzem el mateix "netejador" que l'extracció: treu ```json, text abans/després, etc.
        var json = LlmRespuesta.ExtraerPrimerObjetoJson(respuesta);
        if (json is null)
        {
            logger.LogWarning("Grounding LLM: la respuesta no contiene JSON. Respuesta cruda: {Respuesta}", respuesta);
            return resultado;
        }

        try
        {
            using var documento = JsonDocument.Parse(json);
            if (!documento.RootElement.TryGetProperty("campos", out var campos) || campos.ValueKind != JsonValueKind.Array)
            {
                logger.LogWarning("Grounding LLM: falta el array 'campos'. Respuesta cruda: {Respuesta}", respuesta);
                return resultado;
            }

            foreach (var elemento in campos.EnumerateArray())
            {
                if (LeerElemento(elemento, numPaginas) is { } localizacion)
                {
                    resultado[localizacion.Campo] = (localizacion.Pagina, localizacion.Caja);
                }
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Grounding LLM: JSON malformado. Respuesta cruda: {Respuesta}", respuesta);
        }

        return resultado;
    }

    private static (string Campo, int Pagina, CajaNormalizada Caja)? LeerElemento(JsonElement elemento, int numPaginas)
    {
        if (elemento.ValueKind != JsonValueKind.Object ||
            !elemento.TryGetProperty("campo", out var campo) || campo.ValueKind != JsonValueKind.String ||
            !elemento.TryGetProperty("pagina", out var pagina) || !pagina.TryGetInt32(out var numeroPagina) ||
            !elemento.TryGetProperty("bbox_2d", out var caja) || caja.ValueKind != JsonValueKind.Array ||
            caja.GetArrayLength() != 4)
        {
            return null;
        }

        // Una pàgina que no existeix vol dir que el model s'ha equivocat: millor sense caixa que una caixa a l'aire
        if (numeroPagina < 1 || numeroPagina > numPaginas)
        {
            return null;
        }

        var coordenadas = new double[4];
        var indice = 0;
        foreach (var valor in caja.EnumerateArray())
        {
            if (!valor.TryGetDouble(out var coordenada))
            {
                return null;
            }
            coordenadas[indice++] = coordenada / EscalaCoordenadas;
        }

        // Crear() ordena i limita a 0-1: absorbeix coordenades girades o una mica fora de la imatge
        var cajaNormalizada = CajaNormalizada.Crear(coordenadas[0], coordenadas[1], coordenadas[2], coordenadas[3]);
        return (campo.GetString()!, numeroPagina, cajaNormalizada);
    }
}
