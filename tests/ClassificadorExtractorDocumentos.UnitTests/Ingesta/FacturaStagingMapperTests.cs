using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Application.Ingesta;
using ClassificadorExtractorDocumentos.Application.Validacion;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Entities;
using ClassificadorExtractorDocumentos.UnitTests.Validacion;

namespace ClassificadorExtractorDocumentos.UnitTests.Ingesta;

public class FacturaStagingMapperTests
{
    private static readonly DateTime FechaIngesta = new(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);

    private static readonly DocumentoIngestado Documento =
        new(Guid.Parse("11111111-2222-3333-4444-555555555555"), "C:/docs/f.pdf", ["C:/docs/f-1.png"]);

    private static FacturaStaging Mapear(FacturaExtraida factura) =>
        FacturaStagingMapper.Map(
            Documento,
            ResultadoExtraccion.Ok(factura, "{}", reintentos: 0),
            new ResultadoValidacion(EstadoFactura.Validada, []),
            FechaIngesta);

    [Fact]
    public void Map_persiste_el_receptor_como_cliente()
    {
        var entidad = Mapear(FacturaDePrueba.Valida());

        Assert.Equal("A87654321", entidad.NifCliente);
        Assert.Equal("Cliente SA", entidad.NombreCliente);
    }

    [Fact]
    public void Map_deja_el_cliente_en_null_si_el_documento_no_lo_traia()
    {
        var sinReceptor = FacturaDePrueba.Valida() with { Receptor = new ReceptorExtraido(null, null) };

        var entidad = Mapear(sinReceptor);

        Assert.Null(entidad.NifCliente);
        Assert.Null(entidad.NombreCliente);
    }
}
