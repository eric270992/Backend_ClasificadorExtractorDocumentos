using ClassificadorExtractorDocumentos.Application.Edicion;
using ClassificadorExtractorDocumentos.Application.Validacion;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Entities;
using ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;
using Microsoft.Extensions.Time.Testing;

namespace ClassificadorExtractorDocumentos.UnitTests.Edicion;

/// <summary>
/// Edició manual de capçalera i línies amb revalidació. Es fa servir el ValidadorAgent real (les 9 regles)
/// i un repositori fals que guarda en memòria què s'hauria desat.
/// </summary>
public class FacturaEdicionServiceTests
{
    private const int IdFactura = 7;

    // Factura de 100 + 21% = 121, tal com la va llegir el model (confiances altes)
    private const string JsonOriginal = """
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

    private static FacturaEdicionService Crear(RepositorioFake repo) =>
        new(repo, new ValidadorAgent(
        [
            new ReglaCuadreLineas(), new ReglaCuadreTotal(), new ReglaIvaCoherente(),
            new ReglaReverseCharge(), new ReglaNifFormato(), new ReglaCamposObligatorios(),
            new ReglaConfidenceMinima(), new ReglaFechaRazonable(), new ReglaDuplicado(),
        ]), Reloj);

    /// <summary>Factura en RevisionHumana perquè la línia (90) no quadra amb la base (100).</summary>
    private static FacturaStaging FacturaQueNoCuadra(string jsonOriginal = JsonOriginal) => new()
    {
        Id = IdFactura,
        Proveedor = new Proveedor { Nif = "B12345678", Nombre = "Proveedor SL" },
        NumeroFactura = "F-1",
        FechaFactura = new DateOnly(2026, 7, 1),
        Moneda = "EUR",
        BaseImponible = 100m,
        CuotaIva = 21m,
        Total = 121m,
        Estado = EstadoFactura.RevisionHumana,
        JsonOriginalExtraido = jsonOriginal,
        RutaPdfOriginal = "mem://x.pdf",
        Lineas = [new FacturaLinea { Id = 1, NumLinea = 1, Descripcion = "X", Cantidad = 1, PrecioUnitario = 90, PorcentajeIva = 21, ImporteLinea = 90 }],
        Incidencias = [new ValidacionIncidencia { Codigo = "CUADRE_LINEAS", Detalle = "No cuadra" }],
    };

    /// <summary>Petició amb la mateixa capçalera que la factura (només canvien les línies que es passin).</summary>
    private static ActualizarFacturaRequest Peticion(FacturaStaging f, IReadOnlyList<LineaEditada>? lineas = null,
        string? numero = null, decimal? total = null) =>
        new(f.NifCliente, f.NombreCliente, numero ?? f.NumeroFactura, f.FechaFactura, f.FechaVencimiento, f.Moneda,
            f.BaseImponible, f.CuotaIva, f.RetencionIrpf, total ?? f.Total, f.ReverseCharge, lineas);

    [Fact]
    public async Task Corregir_la_linea_que_no_cuadra_deja_la_factura_Validada()
    {
        var factura = FacturaQueNoCuadra();
        var repo = new RepositorioFake(factura);

        var resultado = await Crear(repo).ActualizarAsync(IdFactura,
            Peticion(factura, [new LineaEditada(1, "X", 1, 100, 21, 100)]));

        Assert.True(resultado.Exito);
        Assert.Equal(EstadoFactura.Validada, resultado.Estado);
        Assert.Empty(resultado.Incidencias);
        Assert.True(repo.Guardada);
        Assert.Contains(repo.Cambios, c => c.Campo == "Línea 1: ImporteLinea" && c.ValorAnterior == "90" && c.ValorNuevo == "100");
        Assert.Contains(repo.Cambios, c => c.Campo == "Estado" && c.ValorNuevo == "Validada");
        Assert.Single(repo.IncidenciasAnteriores); // la incidència vella se substitueix
    }

    [Fact]
    public async Task La_revalidacion_excluye_la_propia_factura_al_buscar_duplicados()
    {
        var factura = FacturaQueNoCuadra();
        var repo = new RepositorioFake(factura);

        await Crear(repo).ActualizarAsync(IdFactura, Peticion(factura, [new LineaEditada(1, "X", 1, 100, 21, 100)]));

        Assert.Equal(IdFactura, repo.IdExcluidoEnDuplicado);
    }

    [Fact]
    public async Task Cambiar_el_numero_a_uno_que_ya_existe_no_guarda_nada()
    {
        var factura = FacturaQueNoCuadra();
        var repo = new RepositorioFake(factura) { HayOtraConMismoNumero = true };

        var resultado = await Crear(repo).ActualizarAsync(IdFactura, Peticion(factura, numero: "F-2"));

        Assert.False(resultado.Exito);
        Assert.Contains(resultado.Incidencias, i => i.Codigo == "DUPLICADO");
        Assert.False(repo.Guardada);
    }

    [Fact]
    public async Task Un_campo_editado_a_mano_ya_no_genera_incidencia_de_confianza()
    {
        // El model va llegir el total amb confiança baixa (0,5)
        var factura = FacturaQueNoCuadra(JsonOriginal.Replace("\"totales.total\": 0.99", "\"totales.total\": 0.5"));
        var repo = new RepositorioFake(factura);

        // L'usuari corregeix la línia a 110 i ajusta base, quota i total: el total ara és un valor revisat
        var peticion = Peticion(factura, [new LineaEditada(1, "X", 1, 110, 21, 110)]) with
        {
            BaseImponible = 110m, CuotaIva = 23.10m, Total = 133.10m,
        };
        var resultado = await Crear(repo).ActualizarAsync(IdFactura, peticion);

        Assert.DoesNotContain(resultado.Incidencias, i => i.Codigo == "CONFIDENCE_MINIMA");
    }

    [Fact]
    public async Task Mismo_importe_con_otros_decimales_no_cuenta_como_cambio()
    {
        // A la BD l'import és 121.00 (decimal(18,2)); del frontend arriba 121
        var factura = FacturaQueNoCuadra();
        factura.Total = 121.00m;
        var repo = new RepositorioFake(factura);

        var resultado = await Crear(repo).ActualizarAsync(IdFactura, Peticion(factura) with { Total = 121m });

        Assert.False(resultado.HuboCambios);
        Assert.False(repo.Guardada);
    }

    [Fact]
    public async Task Sin_ninguna_modificacion_no_se_revalida_ni_se_guarda()
    {
        var factura = FacturaQueNoCuadra();
        var repo = new RepositorioFake(factura);

        var resultado = await Crear(repo).ActualizarAsync(IdFactura,
            Peticion(factura, [new LineaEditada(1, "X", 1, 90, 21, 90)]));

        Assert.True(resultado.Exito);
        Assert.False(resultado.HuboCambios);
        Assert.Equal(EstadoFactura.RevisionHumana, resultado.Estado);
        Assert.False(repo.Guardada);
    }

    [Fact]
    public async Task Anadir_y_eliminar_lineas_renumera_y_lo_registra_en_el_historial()
    {
        var factura = FacturaQueNoCuadra();
        var repo = new RepositorioFake(factura);

        // S'elimina la línia 1 (90) i se n'afegeixen dues de 50 que sí que sumen la base
        var resultado = await Crear(repo).ActualizarAsync(IdFactura, Peticion(factura,
        [
            new LineaEditada(null, "A", 1, 50, 21, 50),
            new LineaEditada(null, "B", 1, 50, 21, 50),
        ]));

        Assert.True(resultado.Exito);
        Assert.Equal(EstadoFactura.Validada, resultado.Estado);
        Assert.Single(repo.LineasEliminadas);
        Assert.Equal([1, 2], factura.Lineas.Select(l => l.NumLinea));
        Assert.Contains(repo.Cambios, c => c.Campo == "Línea eliminada");
        Assert.Equal(2, repo.Cambios.Count(c => c.Campo == "Línea añadida"));
    }

    [Fact]
    public async Task Una_linea_de_otra_factura_se_rechaza()
    {
        var factura = FacturaQueNoCuadra();
        var repo = new RepositorioFake(factura);

        var resultado = await Crear(repo).ActualizarAsync(IdFactura,
            Peticion(factura, [new LineaEditada(999, "X", 1, 100, 21, 100)]));

        Assert.False(resultado.Exito);
        Assert.Contains("999", resultado.Motivo);
        Assert.False(repo.Guardada);
    }

    private sealed class RepositorioFake(FacturaStaging factura) : IFacturaStagingRepository
    {
        public bool HayOtraConMismoNumero { get; init; }
        public int? IdExcluidoEnDuplicado { get; private set; }
        public bool Guardada { get; private set; }
        public IReadOnlyList<FacturaLinea> LineasEliminadas { get; private set; } = [];
        public IReadOnlyList<ValidacionIncidencia> IncidenciasAnteriores { get; private set; } = [];
        public IReadOnlyList<HistorialCambioFactura> Cambios { get; private set; } = [];

        public Task<FacturaStaging?> ObtenerParaEdicionAsync(int facturaId, CancellationToken ct = default) =>
            Task.FromResult<FacturaStaging?>(facturaId == factura.Id ? factura : null);

        public Task<IReadOnlyList<string>> ObtenerCamposEditadosAsync(int facturaId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<bool> ExisteOtraFacturaAsync(string nif, string numero, int excluir, CancellationToken ct = default)
        {
            IdExcluidoEnDuplicado = excluir;
            return Task.FromResult(HayOtraConMismoNumero);
        }

        public Task GuardarEdicionAsync(
            FacturaStaging f, IReadOnlyList<FacturaLinea> lineasEliminadas,
            IReadOnlyList<ValidacionIncidencia> incidenciasAnteriores, IReadOnlyList<HistorialCambioFactura> cambios,
            CancellationToken ct = default)
        {
            Guardada = true;
            LineasEliminadas = lineasEliminadas;
            IncidenciasAnteriores = incidenciasAnteriores;
            Cambios = cambios;
            return Task.CompletedTask;
        }

        // No intervenen en l'edició
        public Task<bool> ExisteFacturaAsync(string nif, string numero, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> GuardarAsync(FacturaStaging f, string? nif, string? nombre, CancellationToken ct = default) => Task.FromResult(0);
        public Task<bool> EliminarAsync(int facturaId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoAprobacion> AprobarAsync(int facturaId, CancellationToken ct = default) => Task.FromResult(ResultadoAprobacion.Ok());
    }
}
