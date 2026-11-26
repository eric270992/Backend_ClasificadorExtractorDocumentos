using ClassificadorExtractorDocumentos.Application.Grounding;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClassificadorExtractorDocumentos.UnitTests.Grounding;

/// <summary>Proves de la lectura de la resposta del model: el que ens interessa és que una resposta parcialment
/// dolenta no faci perdre les caixes bones, i que les coordenades 0-1000 passin a 0-1.</summary>
public class GroundingLlmAgentTests
{
    private static readonly CampoBuscado[] Campos =
    [
        new("totales.total", "Total", "24.20", Numero: 24.20m),
        new("factura.numero", "Número", "F-1", Texto: "F-1"),
        new("factura.fecha", "Data", "2026-07-05", Fecha: new DateOnly(2026, 7, 5)),
    ];

    [Fact]
    public async Task Coordenades_0_1000_es_converteixen_a_0_1()
    {
        var llm = new LlmClientFake("""{"campos":[{"campo":"totales.total","pagina":1,"bbox_2d":[800,900,950,920]}]}""");
        var agente = new GroundingLlmAgent(llm, NullLogger<GroundingLlmAgent>.Instance);

        var resultados = await agente.LocalizarAsync([[1, 2, 3]], Campos);

        var total = resultados.Single(resultado => resultado.Campo == "totales.total");
        Assert.Equal(1, total.Pagina);
        Assert.Equal(0.80, total.Caja!.X1, precision: 6);
        Assert.Equal(0.92, total.Caja.Y2, precision: 6);
    }

    [Fact]
    public async Task Elements_mal_formats_o_amb_pagina_inexistent_queden_sense_caixa_sense_perdre_la_resta()
    {
        // numero: pàgina 3 en un document d'1 pàgina; fecha: bbox de 3 coordenades. Total, correcte i dins ```json
        var llm = new LlmClientFake("""
            ```json
            {"campos":[
              {"campo":"totales.total","pagina":1,"bbox_2d":[950,920,800,900]},
              {"campo":"factura.numero","pagina":3,"bbox_2d":[1,2,3,4]},
              {"campo":"factura.fecha","pagina":1,"bbox_2d":[1,2,3]}
            ]}
            ```
            """);
        var agente = new GroundingLlmAgent(llm, NullLogger<GroundingLlmAgent>.Instance);

        var resultados = await agente.LocalizarAsync([[1, 2, 3]], Campos);

        // Es retornen sempre tots els camps demanats, en el mateix ordre
        Assert.Equal(["totales.total", "factura.numero", "factura.fecha"], resultados.Select(resultado => resultado.Campo));
        // Coordenades girades (x1 > x2): es reordenen en lloc de descartar la caixa
        var total = resultados[0];
        Assert.True(total.Caja!.X1 < total.Caja.X2);
        Assert.Null(resultados[1].Caja);
        Assert.Null(resultados[2].Caja);
    }

    [Fact]
    public async Task Resposta_sense_json_retorna_tots_els_camps_sense_caixa()
    {
        var agente = new GroundingLlmAgent(new LlmClientFake("no sé on és"), NullLogger<GroundingLlmAgent>.Instance);

        var resultados = await agente.LocalizarAsync([[1, 2, 3]], Campos);

        Assert.Equal(Campos.Length, resultados.Count);
        Assert.All(resultados, resultado => Assert.Null(resultado.Caja));
    }

    private sealed class LlmClientFake(string respuesta) : ILlmClient
    {
        public Task<string> CompletarAsync(LlmPeticion peticion, CancellationToken cancellationToken = default) =>
            Task.FromResult(respuesta);
    }
}
