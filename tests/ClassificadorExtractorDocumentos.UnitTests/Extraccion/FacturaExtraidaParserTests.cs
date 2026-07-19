using ClassificadorExtractorDocumentos.Application.Extraccion;

namespace ClassificadorExtractorDocumentos.UnitTests.Extraccion;

public class FacturaExtraidaParserTests
{
    private const string JsonCompleto = """
        {
          "emisor": { "nif": "B-12.345.678", "nombre": "Proveedor SL", "direccion": "C/ Mayor 1" },
          "receptor": { "nif": "A87654321", "nombre": "Cliente SA" },
          "factura": { "numero": "F-2026-001", "fecha": "2026-07-05", "vencimiento": null, "moneda": "EUR" },
          "lineas": [
            { "descripcion": "Servicio X", "cantidad": 2, "precioUnitario": 100.5, "porcentajeIva": 21, "importeLinea": 201.0 }
          ],
          "lineasIncluyenIva": false,
          "totales": { "baseImponible": 201.0, "cuotaIva": 42.21, "retencionIrpf": null, "total": 243.21 },
          "metadatos": {
            "idioma": "es",
            "reverseCharge": false,
            "confidencePorCampo": { "emisor.nif": 0.98, "totales.total": 0.99 }
          }
        }
        """;

    [Fact]
    public void Parse_json_valido_mapea_todos_los_campos()
    {
        var resultado = FacturaExtraidaParser.Parse(JsonCompleto);

        Assert.True(resultado.Exito);
        var f = resultado.Factura!;
        Assert.Equal("B12345678", f.Emisor.Nif);           // NIF normalizado sin guiones/puntos
        Assert.Equal("Proveedor SL", f.Emisor.Nombre);
        Assert.Equal("F-2026-001", f.Factura.Numero);
        Assert.Equal("2026-07-05", f.Factura.Fecha);
        Assert.Equal("EUR", f.Factura.Moneda);
        Assert.Single(f.Lineas);
        Assert.Equal(100.5m, f.Lineas[0].PrecioUnitario);
        Assert.Equal(243.21m, f.Totales.Total);
        Assert.False(f.Metadatos.ReverseCharge);
        Assert.Equal(0.98, f.Metadatos.ConfidencePorCampo["emisor.nif"]);
    }

    [Fact]
    public void Parse_tolera_markdown_fences_y_texto_alrededor()
    {
        var respuesta = $"Aquí tienes el resultado:\n```json\n{JsonCompleto}\n```\nEspero que te sirva.";

        var resultado = FacturaExtraidaParser.Parse(respuesta);

        Assert.True(resultado.Exito);
        Assert.Equal("F-2026-001", resultado.Factura!.Factura.Numero);
    }

    [Fact]
    public void Parse_campo_ausente_es_null_con_confidence_cero()
    {
        var json = """
            {
              "emisor": { "nif": null, "nombre": "Proveedor SL" },
              "factura": { "numero": "F-1" },
              "totales": { "total": null },
              "metadatos": { "confidencePorCampo": {} }
            }
            """;

        var resultado = FacturaExtraidaParser.Parse(json);

        Assert.True(resultado.Exito);
        var f = resultado.Factura!;
        Assert.Null(f.Emisor.Nif);
        Assert.Null(f.Totales.Total);
        Assert.Null(f.Factura.Fecha);
        Assert.Equal(0.0, f.Metadatos.ConfidencePorCampo["emisor.nif"]);
        Assert.Equal(0.0, f.Metadatos.ConfidencePorCampo["totales.total"]);
        Assert.Equal(0.0, f.Metadatos.ConfidencePorCampo["factura.fecha"]);
    }

    [Fact]
    public void Parse_numeros_como_texto_se_normalizan()
    {
        var json = """
            {
              "totales": { "baseImponible": "1.234,56", "total": "1.493,82" },
              "metadatos": {}
            }
            """;

        var resultado = FacturaExtraidaParser.Parse(json);

        Assert.True(resultado.Exito);
        Assert.Equal(1234.56m, resultado.Factura!.Totales.BaseImponible);
        Assert.Equal(1493.82m, resultado.Factura.Totales.Total);
    }

    [Fact]
    public void Parse_fecha_no_iso_se_normaliza()
    {
        var json = """
            { "factura": { "numero": "F-1", "fecha": "05/07/2026" }, "metadatos": {} }
            """;

        var resultado = FacturaExtraidaParser.Parse(json);

        Assert.True(resultado.Exito);
        Assert.Equal("2026-07-05", resultado.Factura!.Factura.Fecha);
    }

    [Theory]
    [InlineData("")]
    [InlineData("No puedo procesar este documento.")]
    [InlineData("{ \"emisor\": { rota }")]
    public void Parse_respuesta_invalida_devuelve_fallo_con_motivo(string respuesta)
    {
        var resultado = FacturaExtraidaParser.Parse(respuesta);

        Assert.False(resultado.Exito);
        Assert.NotNull(resultado.Error);
    }

    [Fact]
    public void Parse_resuelve_suma_sin_evaluar_dejada_por_el_modelo()
    {
        // Bug real visto con Nvidia (nemotron-nano-vl-8b): en vez del total ya calculado, el
        // modelo escribe la operación, lo que rompe el JSON ('+' is invalid after a value).
        var json = """
            {
              "factura": { "numero": "F-1" },
              "totales": { "baseImponible": 255.00 + 189.90 + 190.00, "total": 768.23 },
              "metadatos": {}
            }
            """;

        var resultado = FacturaExtraidaParser.Parse(json);

        Assert.True(resultado.Exito);
        Assert.Equal(634.90m, resultado.Factura!.Totales.BaseImponible);
    }

    [Fact]
    public void Parse_moneda_ausente_por_defecto_eur()
    {
        var json = """{ "factura": { "numero": "F-1" }, "metadatos": {} }""";

        var resultado = FacturaExtraidaParser.Parse(json);

        Assert.Equal("EUR", resultado.Factura!.Factura.Moneda);
    }
}
