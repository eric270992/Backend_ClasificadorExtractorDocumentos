using ClassificadorExtractorDocumentos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence.Configurations;

public class FacturaLineaConfiguration : IEntityTypeConfiguration<FacturaLinea>
{
    public void Configure(EntityTypeBuilder<FacturaLinea> builder)
    {
        builder.ToTable("FacturasLineas");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Descripcion).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Cantidad).HasColumnType("decimal(18,4)");
        builder.Property(l => l.PrecioUnitario).HasColumnType("decimal(18,4)");
        builder.Property(l => l.PorcentajeIva).HasColumnType("decimal(5,2)");
        builder.Property(l => l.ImporteLinea).HasColumnType("decimal(18,2)");
    }
}
