using System.Text.Json;
using ClassificadorExtractorDocumentos.Application.Llm;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Parsers;
using ClassificadorExtractorDocumentos.Domain.ValueObjects;

namespace ClassificadorExtractorDocumentos.Application.Extraccion;

/// <summary>
/// Deserializa la respuesta del LLM al contrato FacturaExtraida (§2.1) de forma tolerante:
/// acepta fences de markdown y texto alrededor, y normaliza números/fechas como red de seguridad.
/// Campo ausente o inválido → null + confidence 0 (nunca inventar).
/// </summary>
public static class FacturaExtraidaParser
{
    public static ResultadoParseo Parse(string respuestaLlm)
    {
        var json = LlmRespuesta.ExtraerPrimerObjetoJson(respuestaLlm);
        if (json is null)
        {
            return ResultadoParseo.Fallo("La respuesta no contiene ningún objeto JSON.");
        }
        json = LlmRespuesta.ResolverSumasSinEvaluar(json);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return ResultadoParseo.Fallo($"JSON malformado: {ex.Message}");
        }

        using (doc)
        {
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
            {
                return ResultadoParseo.Fallo("La raíz del JSON no es un objeto.");
            }

            var confidence = LeerConfidence(raiz);

            var factura = new FacturaExtraida(
                Emisor: new EmisorExtraido(
                    Nif: NormalizarNif(LeerString(raiz, "emisor", "nif")),
                    Nombre: LeerString(raiz, "emisor", "nombre"),
                    Direccion: LeerString(raiz, "emisor", "direccion")),
                Receptor: new ReceptorExtraido(
                    Nif: NormalizarNif(LeerString(raiz, "receptor", "nif")),
                    Nombre: LeerString(raiz, "receptor", "nombre")),
                Factura: new DatosFacturaExtraidos(
                    Numero: LeerString(raiz, "factura", "numero"),
                    Fecha: NormalizarFecha(LeerString(raiz, "factura", "fecha")),
                    Vencimiento: NormalizarFecha(LeerString(raiz, "factura", "vencimiento")),
                    Moneda: LeerString(raiz, "factura", "moneda") ?? "EUR"),
                Lineas: LeerLineas(raiz),
                LineasIncluyenIva: LeerBool(raiz, "lineasIncluyenIva") ?? false,
                Totales: new TotalesExtraidos(
                    BaseImponible: LeerDecimal(raiz, "totales", "baseImponible"),
                    CuotaIva: LeerDecimal(raiz, "totales", "cuotaIva"),
                    RetencionIrpf: LeerDecimal(raiz, "totales", "retencionIrpf"),
                    Total: LeerDecimal(raiz, "totales", "total"),
                    PorcentajeIva: LeerDecimal(raiz, "totales", "porcentajeIva")),
                Metadatos: new MetadatosExtraidos(
                    Idioma: LeerString(raiz, "metadatos", "idioma"),
                    ReverseCharge: LeerBool(raiz, "metadatos", "reverseCharge") ?? false,
                    ConfidencePorCampo: confidence));

            // Contrato §2.1: campo obligatorio ausente → confidence 0 explícita
            var conf = new Dictionary<string, double>(confidence);
            AsegurarConfidenceCero(conf, "emisor.nif", factura.Emisor.Nif);
            AsegurarConfidenceCero(conf, "emisor.nombre", factura.Emisor.Nombre);
            AsegurarConfidenceCero(conf, "factura.numero", factura.Factura.Numero);
            AsegurarConfidenceCero(conf, "factura.fecha", factura.Factura.Fecha);
            AsegurarConfidenceCero(conf, "totales.total", factura.Totales.Total);

            return ResultadoParseo.Ok(factura with
            {
                Metadatos = factura.Metadatos with { ConfidencePorCampo = conf },
            }, json);
        }
    }

    private static string? LeerString(JsonElement raiz, params string[] camino)
    {
        var el = Navegar(raiz, camino);
        return el?.ValueKind switch
        {
            JsonValueKind.String => VacioComoNull(el.Value.GetString()),
            JsonValueKind.Number => el.Value.GetRawText(),
            _ => null,
        };
    }

    private static decimal? LeerDecimal(JsonElement raiz, params string[] camino)
    {
        var el = Navegar(raiz, camino);
        return el?.ValueKind switch
        {
            JsonValueKind.Number => el.Value.GetDecimal(),
            // Red de seguridad: si el modelo devuelve el número como texto ("1.234,56"), normalizar
            JsonValueKind.String => NumeroParser.Parse(el.Value.GetString()),
            _ => null,
        };
    }

    private static bool? LeerBool(JsonElement raiz, params string[] camino)
    {
        var el = Navegar(raiz, camino);
        return el?.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static JsonElement? Navegar(JsonElement raiz, params string[] camino)
    {
        var actual = raiz;
        foreach (var paso in camino)
        {
            if (actual.ValueKind != JsonValueKind.Object || !actual.TryGetProperty(paso, out var siguiente))
            {
                return null;
            }
            actual = siguiente;
        }
        return actual.ValueKind == JsonValueKind.Null ? null : actual;
    }

    private static IReadOnlyList<LineaExtraida> LeerLineas(JsonElement raiz)
    {
        if (!raiz.TryGetProperty("lineas", out var lineas) || lineas.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var resultado = new List<LineaExtraida>();
        foreach (var linea in lineas.EnumerateArray())
        {
            if (linea.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            resultado.Add(new LineaExtraida(
                Descripcion: LeerString(linea, "descripcion"),
                Cantidad: LeerDecimal(linea, "cantidad"),
                PrecioUnitario: LeerDecimal(linea, "precioUnitario"),
                PorcentajeIva: LeerDecimal(linea, "porcentajeIva"),
                ImporteLinea: LeerDecimal(linea, "importeLinea")));
        }
        return resultado;
    }

    private static IReadOnlyDictionary<string, double> LeerConfidence(JsonElement raiz)
    {
        var resultado = new Dictionary<string, double>();
        var el = Navegar(raiz, "metadatos", "confidencePorCampo");
        if (el?.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.Value.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number)
                {
                    resultado[prop.Name] = Math.Clamp(prop.Value.GetDouble(), 0.0, 1.0);
                }
            }
        }
        return resultado;
    }

    private static void AsegurarConfidenceCero(Dictionary<string, double> conf, string campo, object? valor)
    {
        if (valor is null)
        {
            conf[campo] = 0.0;
        }
    }

    private static string? NormalizarNif(string? nif) => Nif.Normalizar(nif);

    private static string? NormalizarFecha(string? fecha) => FechaParser.Parse(fecha);

    private static string? VacioComoNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}

public sealed record ResultadoParseo(bool Exito, FacturaExtraida? Factura, string? JsonOriginal, string? Error)
{
    public static ResultadoParseo Ok(FacturaExtraida factura, string json) => new(true, factura, json, null);
    public static ResultadoParseo Fallo(string error) => new(false, null, null, error);
}
