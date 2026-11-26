using ClassificadorExtractorDocumentos.Application.Grounding;
using ClassificadorExtractorDocumentos.Domain.Grounding;

namespace ClassificadorExtractorDocumentos.UnitTests.Grounding;

/// <summary>
/// Proves del grounding híbrid amb paraules inventades (sense PDF): es construeix una "pàgina" amb paraules i
/// posicions conegudes i es comprova que cada valor es troba a la caixa que toca.
/// </summary>
public class LocalizadorHibridoTests
{
    /// <summary>Alçada d'una fila de text a les pàgines de prova (coordenades 0-1).</summary>
    private const double AltoFila = 0.015;

    /// <summary>Crea una paraula a la fila <paramref name="fila"/> (0 = dalt) començant a la X indicada.
    /// L'amplada és proporcional al nombre de caràcters, que és suficient per a aquestes proves.</summary>
    private static PalabraDocumento Palabra(string texto, int fila, double x)
    {
        var y = 0.05 + fila * 0.03;
        return new PalabraDocumento(texto, new CajaNormalizada(x, y, x + texto.Length * 0.008, y + AltoFila));
    }

    /// <summary>Factura mínima: capçalera, dues línies amb el MATEIX import i un peu on la base també és 10,00.</summary>
    private static IReadOnlyList<PaginaPalabras> FacturaDePrueba() =>
    [
        new PaginaPalabras(1, 0,
        [
            Palabra("ELECTRÒNICA", 0, 0.05), Palabra("OLOT,", 0, 0.15), Palabra("S.L.", 0, 0.20),
            Palabra("NIF:", 1, 0.05), Palabra("B-12.345.678", 1, 0.09),
            Palabra("Factura", 2, 0.60), Palabra("Nº", 2, 0.67), Palabra("FAC-2026/001", 2, 0.70),
            Palabra("Fecha:", 3, 0.60), Palabra("05/07/2026", 3, 0.67),
            // Línies: totes dues de 10,00 €. La segona descripció és lleugerament diferent del valor extret.
            Palabra("Cable", 5, 0.05), Palabra("HDMI", 5, 0.10), Palabra("2m", 5, 0.15), Palabra("10,00", 5, 0.80),
            Palabra("Adaptador", 6, 0.05), Palabra("USB-C", 6, 0.14), Palabra("10,00", 6, 0.80),
            // Peu: IVA 21% (el 21 no ha de confondre's amb cap import) i totals
            Palabra("Base", 8, 0.60), Palabra("imponible:", 8, 0.65), Palabra("20,00", 8, 0.80),
            Palabra("IVA", 9, 0.60), Palabra("21%:", 9, 0.65), Palabra("4,20", 9, 0.80),
            Palabra("TOTAL:", 10, 0.60), Palabra("24,20", 10, 0.80), Palabra("€", 10, 0.85),
        ]),
    ];

    private static CampoLocalizado Buscar(IReadOnlyList<CampoLocalizado> resultados, string campo) =>
        resultados.Single(resultado => resultado.Campo == campo);

    [Fact]
    public void Nif_amb_guions_i_punts_al_document_es_troba_igualment()
    {
        var campos = new[] { new CampoBuscado("emisor.nif", "NIF", "B12345678", Texto: "B12345678") };

        var resultado = Buscar(LocalizadorHibrido.Localizar(FacturaDePrueba(), campos), "emisor.nif");

        Assert.Equal(1, resultado.Pagina);
        Assert.NotNull(resultado.Caja);
        // La caixa és la de la paraula "B-12.345.678" (x = 0.09), no la de l'etiqueta "NIF:"
        Assert.Equal(0.09, resultado.Caja!.X1, precision: 3);
    }

    [Fact]
    public void Nom_amb_accents_i_puntuacio_diferents_es_troba_per_aproximacio()
    {
        var campos = new[]
        {
            new CampoBuscado("emisor.nombre", "Nom", "Electronica Olot SL", Texto: "Electronica Olot SL", Aproximado: true),
        };

        var resultado = Buscar(LocalizadorHibrido.Localizar(FacturaDePrueba(), campos), "emisor.nombre");

        Assert.NotNull(resultado.Caja);
        // La caixa ha d'abraçar les tres paraules del nom: comença a "ELECTRÒNICA" i acaba a "S.L."
        Assert.Equal(0.05, resultado.Caja!.X1, precision: 3);
        Assert.True(resultado.Caja.X2 > 0.20);
    }

    [Fact]
    public void Data_iso_es_troba_en_format_dd_mm_aaaa()
    {
        var campos = new[] { new CampoBuscado("factura.fecha", "Data", "2026-07-05", Fecha: new DateOnly(2026, 7, 5)) };

        var resultado = Buscar(LocalizadorHibrido.Localizar(FacturaDePrueba(), campos), "factura.fecha");

        Assert.NotNull(resultado.Caja);
        Assert.Equal(0.67, resultado.Caja!.X1, precision: 3);
    }

    [Fact]
    public void Total_es_troba_i_la_caixa_no_inclou_el_simbol_de_moneda()
    {
        var campos = new[] { new CampoBuscado("totales.total", "Total", "24.20", Numero: 24.20m, PreferirUltimo: true) };

        var resultado = Buscar(LocalizadorHibrido.Localizar(FacturaDePrueba(), campos), "totales.total");

        Assert.NotNull(resultado.Caja);
        Assert.Equal(1, resultado.NumCandidatos);
        // "24,20" sol ja coincideix: el n-grama "24,20 €" el conté i es descarta (caixa més ajustada)
        Assert.True(resultado.Caja!.X2 < 0.85);
    }

    [Fact]
    public void Percentatge_no_es_confon_amb_un_import()
    {
        // Busquem un import de 21: l'únic "21" del document és "21%:", que no s'ha d'acceptar
        var campos = new[] { new CampoBuscado("prova", "Prova", "21.00", Numero: 21m) };

        var resultado = Buscar(LocalizadorHibrido.Localizar(FacturaDePrueba(), campos), "prova");

        Assert.Null(resultado.Caja);
        Assert.Equal(0, resultado.NumCandidatos);
    }

    [Fact]
    public void Imports_repetits_de_linia_van_a_la_fila_de_la_seva_descripcio()
    {
        // Dues línies de 10,00: sense la pista MismaFilaQue, totes dues caixes anirien a la primera aparició
        var campos = new[]
        {
            new CampoBuscado("lineas[0].descripcion", "L1", "Cable HDMI 2m", Texto: "Cable HDMI 2m", Aproximado: true),
            new CampoBuscado("lineas[0].importeLinea", "L1 import", "10.00", Numero: 10m, MismaFilaQue: "lineas[0].descripcion"),
            new CampoBuscado("lineas[1].descripcion", "L2", "Adaptador USB C", Texto: "Adaptador USB C", Aproximado: true,
                DespuesDe: "lineas[0].descripcion"),
            new CampoBuscado("lineas[1].importeLinea", "L2 import", "10.00", Numero: 10m, MismaFilaQue: "lineas[1].descripcion"),
        };

        var resultados = LocalizadorHibrido.Localizar(FacturaDePrueba(), campos);

        var importeLinea1 = Buscar(resultados, "lineas[0].importeLinea");
        var importeLinea2 = Buscar(resultados, "lineas[1].importeLinea");
        Assert.Equal(2, importeLinea1.NumCandidatos);
        // Cada import a la seva fila: la Y de la caixa coincideix amb la de la seva descripció
        Assert.Equal(Buscar(resultados, "lineas[0].descripcion").Caja!.Y1, importeLinea1.Caja!.Y1, precision: 6);
        Assert.Equal(Buscar(resultados, "lineas[1].descripcion").Caja!.Y1, importeLinea2.Caja!.Y1, precision: 6);
        Assert.NotEqual(importeLinea1.Caja.Y1, importeLinea2.Caja.Y1);
    }

    [Fact]
    public void Valor_que_no_surt_al_document_queda_sense_caixa()
    {
        // Un valor que el model s'hagués inventat: és justament el que el grounding ha de fer evident
        var campos = new[] { new CampoBuscado("factura.numero", "Número", "FAC-2026/999", Texto: "FAC-2026/999") };

        var resultado = Buscar(LocalizadorHibrido.Localizar(FacturaDePrueba(), campos), "factura.numero");

        Assert.Null(resultado.Pagina);
        Assert.Null(resultado.Caja);
    }
}
