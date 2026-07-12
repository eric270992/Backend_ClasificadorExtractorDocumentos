using ClassificadorExtractorDocumentos.Application.Consultor;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace ClassificadorExtractorDocumentos.UnitTests.Consultor;

public class ConsultorAgentTests
{
    private static readonly FakeTimeProvider Reloj = new(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Pregunta_valida_ejecuta_sql_y_redacta_respuesta()
    {
        var llm = new LlmFake(
            """{ "sql": "SELECT Nombre FROM Proveedores", "explicacion": "Lista los proveedores" }""",
            "Tienes 2 proveedores: A y B.");
        var ejecutor = new EjecutorFake();
        var agente = new ConsultorAgent(llm, ejecutor, Reloj);

        var r = await agente.PreguntarAsync("¿qué proveedores tenemos?");

        Assert.True(r.Exito);
        Assert.Equal("Tienes 2 proveedores: A y B.", r.Respuesta);
        Assert.StartsWith("SELECT TOP 1000 ", r.Sql);           // el guard forzó el límite
        Assert.Equal(r.Sql, ejecutor.SqlEjecutado);              // se ejecutó el SQL asegurado
        Assert.NotNull(r.Datos);
    }

    [Fact]
    public async Task Sql_peligroso_del_llm_se_bloquea_antes_de_la_bd()
    {
        // El "borra las facturas" del criterio de aceptación: aunque el LLM obedeciera y
        // generase un DELETE, el guard lo corta y el ejecutor JAMÁS recibe la consulta.
        var llm = new LlmFake(
            """{ "sql": "DELETE FROM FacturasStaging", "explicacion": "Borra las facturas" }""");
        var ejecutor = new EjecutorFake();
        var agente = new ConsultorAgent(llm, ejecutor, Reloj);

        var r = await agente.PreguntarAsync("borra todas las facturas");

        Assert.False(r.Exito);
        Assert.Contains("seguridad", r.Motivo);
        Assert.Null(ejecutor.SqlEjecutado);      // nunca llegó a la BD
        Assert.Equal(1, llm.Llamadas);           // tampoco se redactó respuesta
    }

    [Fact]
    public async Task Si_el_modelo_declina_se_devuelve_su_motivo()
    {
        var llm = new LlmFake(
            """{ "sql": "", "explicacion": "Solo puedo consultar datos, no modificarlos." }""");
        var ejecutor = new EjecutorFake();
        var agente = new ConsultorAgent(llm, ejecutor, Reloj);

        var r = await agente.PreguntarAsync("elimina al proveedor X");

        Assert.False(r.Exito);
        Assert.Contains("no modificarlos", r.Motivo);
        Assert.Null(ejecutor.SqlEjecutado);
    }

    [Fact]
    public async Task Error_de_ejecucion_reintenta_una_vez_con_feedback()
    {
        var llm = new LlmFake(
            """{ "sql": "SELECT Nombre FROM Proveedores WHERE {mal}", "explicacion": "x" }""",
            """{ "sql": "SELECT Nombre FROM Proveedores", "explicacion": "corregida" }""",
            "Un proveedor: A.");
        var ejecutor = new EjecutorFake { FallarPrimeraVez = true };
        var agente = new ConsultorAgent(llm, ejecutor, Reloj);

        var r = await agente.PreguntarAsync("¿proveedores?");

        Assert.True(r.Exito);
        Assert.Equal(2, ejecutor.Ejecuciones);   // falló + reintento corregido
    }

    [Fact]
    public async Task Dos_errores_de_ejecucion_devuelven_rechazo_controlado()
    {
        var llm = new LlmFake("""{ "sql": "SELECT x FROM Proveedores", "explicacion": "x" }""");
        var ejecutor = new EjecutorFake { FallarSiempre = true };
        var agente = new ConsultorAgent(llm, ejecutor, Reloj);

        var r = await agente.PreguntarAsync("¿proveedores?");

        Assert.False(r.Exito);
        Assert.Contains("falló al ejecutarse", r.Motivo);
        Assert.Equal(2, ejecutor.Ejecuciones);
    }

    [Fact]
    public async Task Respuesta_llm_sin_json_se_rechaza_con_motivo()
    {
        var llm = new LlmFake("no tengo ni idea");
        var agente = new ConsultorAgent(llm, new EjecutorFake(), Reloj);

        var r = await agente.PreguntarAsync("¿cuántas facturas hay?");

        Assert.False(r.Exito);
        Assert.NotNull(r.Motivo);
    }

    private sealed class LlmFake(params string[] respuestas) : ILlmClient
    {
        private int _indice;
        public int Llamadas { get; private set; }

        public Task<string> CompletarAsync(LlmPeticion peticion, CancellationToken ct = default)
        {
            Llamadas++;
            var r = respuestas[Math.Min(_indice, respuestas.Length - 1)];
            _indice++;
            return Task.FromResult(r);
        }
    }

    private sealed class EjecutorFake : IConsultaSqlEjecutor
    {
        public bool FallarPrimeraVez { get; init; }
        public bool FallarSiempre { get; init; }
        public string? SqlEjecutado { get; private set; }
        public int Ejecuciones { get; private set; }

        public Task<ResultadoConsultaSql> EjecutarSelectAsync(string sql, CancellationToken ct = default)
        {
            Ejecuciones++;
            if (FallarSiempre || (FallarPrimeraVez && Ejecuciones == 1))
            {
                throw new InvalidOperationException("Incorrect syntax near '{'.");
            }
            SqlEjecutado = sql;
            return Task.FromResult(new ResultadoConsultaSql(
                ["Nombre"],
                [new Dictionary<string, object?> { ["Nombre"] = "Proveedor A" }]));
        }
    }
}
