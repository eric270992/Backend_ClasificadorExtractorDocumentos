using ClassificadorExtractorDocumentos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence.Configurations;

public class ProveedorEjemploConfiguration : IEntityTypeConfiguration<ProveedorEjemplo>
{
    public void Configure(EntityTypeBuilder<ProveedorEjemplo> builder)
    {
        builder.ToTable("ProveedorEjemplos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RutaImagen).HasMaxLength(500).IsRequired();
        builder.Property(e => e.JsonValidado).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(e => e.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(e => e.Proveedor)
            .WithMany(p => p.Ejemplos)
            .HasForeignKey(e => e.ProveedorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
