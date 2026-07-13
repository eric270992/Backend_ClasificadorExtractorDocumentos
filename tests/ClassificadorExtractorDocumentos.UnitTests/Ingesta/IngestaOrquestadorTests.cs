using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Application.Ingesta;
using ClassificadorExtractorDocumentos.Application.Validacion;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Entities;
using ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;
using Microsoft.Extensions.Time.Testing;

namespace ClassificadorExtractorDocumentos.UnitTests.Ingesta;

public class IngestaOrquestadorTests
{
    private const string JsonFacturaValida = """
        {
          "emisor": { "nif": "B12345678", "nombre": "Proveedor SL" },
          "factura": { "numero": "F-1", "fecha": "2026-07-01", "moneda": "EUR" },
          "lineas": [ { "descripcion": "X", "cantidad": 1, "precioUnitario": 100, "porcentajeIva": 21, "importeLinea": 100 } ],
          "lineasIncluyenIva": false,
          "totales": { "baseImponible": 100, "cuotaIva": 21, "total": 121 },
          "metadatos": { "idioma": "es", "reverseCharge": false, "confidencePorCampo": {
            "emisor.nif": 0.98, "factura.numero": 0.97, "factura.fecha": 0.99, "totales.total": 0.99 } }
        }
        """;

    private static readonly FakeTimeProvider Reloj = new(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero));

    private static IngestaOrquestador CrearOrquestador(string respuestaLlm, RepositorioFake repo)
    {
        var ingesta = new IngestaDocumentoService(new ConversorFake(), new NormalizerFake(), new StorageFake());
        var extractor = new ExtractorAgent(new LlmFake(respuestaLlm));
        var validador = new ValidadorAgent(
        [
            new ReglaCuadreLineas(), new ReglaCuadreTotal(), new ReglaIvaCoherente(),
            new ReglaReverseCharge(), new ReglaNifFormato(), new ReglaCamposObligatorios(),
            new ReglaConfidenceMinima(), new ReglaFechaRazonable(), new ReglaDuplicado(),
        ]);
        return new IngestaOrquestador(ingesta, extractor, validador, repo, Reloj);
    }

    [Fact]
    public async Task Flujo_completo_persiste_factura_validada()
    {
        var repo = new RepositorioFake();
        var orquestador = CrearOrquestador(JsonFacturaValida, repo);

        var resultado = await orquestador.ProcesarAsync(new MemoryStream([0x25, 0x50, 0x44, 0x46]));

        Assert.Null(resultado.Error);
        Assert.Equal(EstadoFactura.Validada, resultado.Estado);
        Assert.NotNull(repo.Guardada);
        Assert.Equal("F-1", repo.Guardada!.NumeroFactura);
        Assert.Equal("B12345678", repo.NifProveedor);
        Assert.Single(repo.Guardada.Lineas);
        Assert.Equal(3, repo.Guardada.NivelExtraccion);
    }

    [Fact]
    public async Task Duplicado_se_persiste_como_rechazada_con_incidencia()
    {
        var repo = new RepositorioFake { SimularDuplicado = true };
        var orquestador = CrearOrquestador(JsonFacturaValida, repo);

        var resultado = await orquestador.ProcesarAsync(new MemoryStream([0x25, 0x50, 0x44, 0x46]));

        Assert.Equal(EstadoFactura.Rechazada, resultado.Estado);
        Assert.Contains(resultado.Incidencias, i => i.Codigo == "DUPLICADO");
        Assert.NotNull(repo.Guardada); // la rechazada también se persiste, con sus incidencias
        Assert.Contains(repo.Guardada!.Incidencias, i => i.Codigo == "DUPLICADO");
    }

    [Fact]
    public async Task Extraccion_fallida_no_persiste_nada()
    {
        var repo = new RepositorioFake();
        var orquestador = CrearOrquestador("respuesta basura sin json", repo);

        var resultado = await orquestador.ProcesarAsync(new MemoryStream([0x25, 0x50, 0x44, 0x46]));

        Assert.NotNull(resultado.Error);
        Assert.Null(resultado.FacturaId);
        Assert.Null(repo.Guardada);
    }

    [Fact]
    public async Task Error_al_persistir_se_propaga_sin_estado_intermedio()
    {
        // La atomicidad real la da la transacción del repositorio; aquí verificamos que el
        // orquestador no captura la excepción ni devuelve un resultado a medias.
        var repo = new RepositorioFake { FallarAlGuardar = true };
        var orquestador = CrearOrquestador(JsonFacturaValida, repo);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orquestador.ProcesarAsync(new MemoryStream([0x25, 0x50, 0x44, 0x46])));
    }

    // ── Fakes ──────────────────────────────────────────────────────────────

    private sealed class LlmFake(string respuesta) : ILlmClient
    {
        public Task<string> CompletarAsync(LlmPeticion peticion, CancellationToken ct = default) =>
            Task.FromResult(respuesta);
    }

    private sealed class ConversorFake : IPdfToImageConverter
    {
        public Task<IReadOnlyList<byte[]>> ConvertToPngPagesAsync(Stream pdfStream, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<byte[]>>([[1, 2, 3]]);
    }

    private sealed class NormalizerFake : IImagenNormalizer
    {
        public Task<byte[]> NormalizarAPngAsync(byte[] imagen, CancellationToken ct = default) =>
            Task.FromResult<byte[]>([1, 2, 3]);
    }

    private sealed class StorageFake : IDocumentStorage
    {
        private IReadOnlyList<byte[]>? _paginas;

        public Task<(string, IReadOnlyList<string>)> GuardarDocumentoAsync(
            Guid id, byte[] original, string extensionOriginal, IReadOnlyList<byte[]> paginas, CancellationToken ct = default)
        {
            _paginas = paginas;
            return Task.FromResult(($"mem://{id}/original.{extensionOriginal}", (IReadOnlyList<string>)[$"mem://{id}/page-1.png"]));
        }

        public Task<IReadOnlyList<byte[]>?> ObtenerImagenesAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_paginas);
    }

    private sealed class RepositorioFake : IFacturaStagingRepository
    {
        public bool SimularDuplicado { get; init; }
        public bool FallarAlGuardar { get; init; }
        public FacturaStaging? Guardada { get; private set; }
        public string? NifProveedor { get; private set; }

        public Task<bool> ExisteFacturaAsync(string nif, string numero, CancellationToken ct = default) =>
            Task.FromResult(SimularDuplicado);

        public Task<int> GuardarAsync(FacturaStaging factura, string? nif, string? nombre, CancellationToken ct = default)
        {
            if (FallarAlGuardar)
            {
                throw new InvalidOperationException("Fallo simulado de base de datos.");
            }
            Guardada = factura;
            NifProveedor = nif;
            return Task.FromResult(42);
        }
    }
}
