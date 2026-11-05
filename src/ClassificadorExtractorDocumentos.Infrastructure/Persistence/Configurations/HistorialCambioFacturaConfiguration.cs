using ClassificadorExtractorDocumentos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence.Configurations;

public class HistorialCambioFacturaConfiguration : IEntityTypeConfiguration<HistorialCambioFactura>
{
    public void Configure(EntityTypeBuilder<HistorialCambioFactura> builder)
    {
        builder.ToTable("HistorialCambiosFactura");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Campo).HasMaxLength(50).IsRequired();
        builder.Property(h => h.ValorAnterior).HasMaxLength(500);
        builder.Property(h => h.ValorNuevo).HasMaxLength(500);
        builder.Property(h => h.Usuario).HasMaxLength(100).IsRequired();
        builder.Property(h => h.FechaCambio).HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();

        // Sin navegación en FacturaStaging a propósito: es un log de auditoría de solo escritura
        // desde el dominio, se consulta aparte (FacturaConsultaService), no forma parte del agregado.
        builder.HasOne<FacturaStaging>()
            .WithMany()
            .HasForeignKey(h => h.FacturaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => h.FacturaId);
    }
}
