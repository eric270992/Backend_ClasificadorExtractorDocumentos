using ClassificadorExtractorDocumentos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence.Configurations;

public class FacturaStagingConfiguration : IEntityTypeConfiguration<FacturaStaging>
{
    public void Configure(EntityTypeBuilder<FacturaStaging> builder)
    {
        builder.ToTable("FacturasStaging");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.NumeroFactura).HasMaxLength(50).IsRequired();
        builder.Property(f => f.FechaFactura).HasColumnType("date").IsRequired();
        builder.Property(f => f.FechaVencimiento).HasColumnType("date");
        builder.Property(f => f.Moneda).HasColumnType("char(3)").HasDefaultValue("EUR").IsRequired();
        builder.Property(f => f.BaseImponible).HasColumnType("decimal(18,2)");
        builder.Property(f => f.CuotaIva).HasColumnType("decimal(18,2)");
        builder.Property(f => f.RetencionIrpf).HasColumnType("decimal(18,2)");
        builder.Property(f => f.Total).HasColumnType("decimal(18,2)");
        builder.Property(f => f.ReverseCharge).HasDefaultValue(false);

        builder.Property(f => f.Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(EstadoFactura.PendienteValidacion)
            .IsRequired();

        builder.Property(f => f.JsonOriginalExtraido).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(f => f.NivelExtraccion).HasColumnType("tinyint");
        builder.Property(f => f.RutaPdfOriginal).HasMaxLength(500).IsRequired();
        builder.Property(f => f.FechaIngesta).HasDefaultValueSql("SYSUTCDATETIME()");

        // Único filtrado: protege contra duplicados vivos pero permite persistir los intentos
        // Rechazados (DUPLICADO) con sus incidencias, como exige §2.3 ("incidencias siempre persistidas")
        builder.HasIndex(f => new { f.ProveedorId, f.NumeroFactura })
            .IsUnique()
            .HasDatabaseName("UQ_Factura")
            .HasFilter("[ProveedorId] IS NOT NULL AND [Estado] <> N'Rechazada'");

        builder.HasOne(f => f.Proveedor)
            .WithMany(p => p.Facturas)
            .HasForeignKey(f => f.ProveedorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(f => f.Lineas)
            .WithOne(l => l.Factura)
            .HasForeignKey(l => l.FacturaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.Incidencias)
            .WithOne(i => i.Factura)
            .HasForeignKey(i => i.FacturaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
