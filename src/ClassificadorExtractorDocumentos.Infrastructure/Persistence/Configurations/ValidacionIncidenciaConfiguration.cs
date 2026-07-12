using ClassificadorExtractorDocumentos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence.Configurations;

public class ValidacionIncidenciaConfiguration : IEntityTypeConfiguration<ValidacionIncidencia>
{
    public void Configure(EntityTypeBuilder<ValidacionIncidencia> builder)
    {
        builder.ToTable("ValidacionIncidencias");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(i => i.Detalle).HasMaxLength(1000).IsRequired();
        builder.Property(i => i.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
