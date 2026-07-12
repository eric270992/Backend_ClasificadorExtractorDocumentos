using ClassificadorExtractorDocumentos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence;

public class DocFlowDbContext(DbContextOptions<DocFlowDbContext> options) : DbContext(options)
{
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<ProveedorEjemplo> ProveedorEjemplos => Set<ProveedorEjemplo>();
    public DbSet<FacturaStaging> FacturasStaging => Set<FacturaStaging>();
    public DbSet<FacturaLinea> FacturasLineas => Set<FacturaLinea>();
    public DbSet<ValidacionIncidencia> ValidacionIncidencias => Set<ValidacionIncidencia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocFlowDbContext).Assembly);
    }
}
