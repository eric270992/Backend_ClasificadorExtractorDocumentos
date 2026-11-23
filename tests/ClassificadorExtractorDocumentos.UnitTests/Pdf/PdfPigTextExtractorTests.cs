using ClassificadorExtractorDocumentos.Infrastructure.Pdf;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace ClassificadorExtractorDocumentos.UnitTests.Pdf;

/// <summary>Test de integración con un PDF real de Crystal Reports (factura COEVA 604104523), donde
/// <c>Page.Text</c> pegaba los números de una línea a la descripción de la siguiente.</summary>
/// <summary>El PDF es una factura real y no se versiona (ver .gitignore): sin él, los tests se omiten
/// en lugar de fallar. Pendiente de sustituirlo por una versión anonimizada.</summary>
public sealed class FactSiExisteElPdfAttribute : FactAttribute
{
    public FactSiExisteElPdfAttribute()
    {
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "Fixtures", "0100604104523Cert.pdf")))
        {
            Skip = "Falta tests/Fixtures/0100604104523Cert.pdf (factura real no versionada).";
        }
    }
}

public class PdfPigTextExtractorTests(ITestOutputHelper salida)
{
    private static readonly string RutaPdf =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "0100604104523Cert.pdf");

    private async Task<string> ExtraerAsync()
    {
        var extractor = new PdfPigTextExtractor(NullLogger<PdfPigTextExtractor>.Instance);
        await using var stream = File.OpenRead(RutaPdf);
        var texto = await extractor.ExtraerTextoAsync(stream);
        Assert.NotNull(texto);

        // Salida para revisar visualmente la alineación (dotnet test --logger "console;verbosity=detailed").
        var pagina1 = texto;
        salida.WriteLine(pagina1);
        return texto;
    }

    private static bool MismaFila(string texto, string a, string b) =>
        texto.Split('\n').Any(linea => linea.Contains(a) && linea.Contains(b));

    [FactSiExisteElPdf]
    public async Task Descripcion_e_importe_de_una_linea_quedan_en_la_misma_fila()
    {
        var texto = await ExtraerAsync();

        Assert.True(MismaFila(texto, "RELE INDUST.RCM570L24", "15,75"));
        Assert.True(MismaFila(texto, "Conexión transversal ZQV 4N/10", "4,43"));
    }

    [FactSiExisteElPdf]
    public async Task No_hay_columnas_pegadas()
    {
        var texto = await ExtraerAsync();

        Assert.DoesNotContain("10025,0", texto);
        Assert.DoesNotContain("174,0", texto);
    }

    [FactSiExisteElPdf]
    public async Task Marca_las_paginas_y_conserva_el_contenido_de_la_segunda()
    {
        var texto = await ExtraerAsync();

        Assert.Contains("[Página 2]", texto);
        Assert.Contains("FUSIBLE ELECTRONIC REX12", texto);
    }
}
