using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClassificadorExtractorDocumentos.UnitTests.Extraccion;

public class ExtractorAgentTests
{
    private const string JsonValido = """
        { "factura": { "numero": "F-1", "fecha": "2026-07-05" }, "totales": { "total": 100 }, "metadatos": {} }
        """;

    [Fact]
    public async Task Extraer_con_respuesta_valida_no_reintenta()
    {
        var llm = new LlmClientFake(JsonValido);
        var agente = new ExtractorAgent(llm, NullLogger<ExtractorAgent>.Instance);

        var resultado = await agente.ExtraerAsync([[1, 2, 3]]);

        Assert.True(resultado.Exito);
        Assert.Equal(0, resultado.Reintentos);
        Assert.Equal(1, llm.Llamadas);
        Assert.Equal("F-1", resultado.Factura!.Factura.Numero);
    }

    [Fact]
    public async Task Extraer_json_invalido_reintenta_una_vez_con_feedback()
    {
        var llm = new LlmClientFake("esto no es json", JsonValido);
        var agente = new ExtractorAgent(llm, NullLogger<ExtractorAgent>.Instance);

        var resultado = await agente.ExtraerAsync([[1, 2, 3]]);

        Assert.True(resultado.Exito);
        Assert.Equal(1, resultado.Reintentos);
        Assert.Equal(2, llm.Llamadas);
        Assert.Contains("no cumplía el contrato", llm.UltimoPromptUsuario);
    }

    [Fact]
    public async Task Extraer_dos_fallos_devuelve_error_controlado()
    {
        var llm = new LlmClientFake("basura", "más basura");
        var agente = new ExtractorAgent(llm, NullLogger<ExtractorAgent>.Instance);

        var resultado = await agente.ExtraerAsync([[1, 2, 3]]);

        Assert.False(resultado.Exito);
        Assert.Equal(2, llm.Llamadas);
        Assert.Contains("reintento", resultado.Error);
    }

    private sealed class LlmClientFake(params string[] respuestas) : ILlmClient
    {
        private int _indice;

        public int Llamadas { get; private set; }
        public string UltimoPromptUsuario { get; private set; } = "";

        public Task<string> CompletarAsync(LlmPeticion peticion, CancellationToken cancellationToken = default)
        {
            Llamadas++;
            UltimoPromptUsuario = peticion.PromptUsuario;
            var respuesta = respuestas[Math.Min(_indice, respuestas.Length - 1)];
            _indice++;
            return Task.FromResult(respuesta);
        }
    }
}
