using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Infrastructure.Consultor;
using ClassificadorExtractorDocumentos.Infrastructure.Llm;
using ClassificadorExtractorDocumentos.Infrastructure.Pdf;
using ClassificadorExtractorDocumentos.Infrastructure.Persistence;
using ClassificadorExtractorDocumentos.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClassificadorExtractorDocumentos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Selección de base de datos por nombre (mismo patrón que el proveedor LLM). "LocalDb" (dev,
        // por defecto) o "SqlServer" (despliegue). Se elige con Database:Proveedor — vía appsettings,
        // variable de entorno Database__Proveedor o argumento --db. La cadena se lee de ConnectionStrings:{nombre}.
        var proveedorBd = configuration["Database:Proveedor"] ?? "LocalDb";
        var connectionString = configuration.GetConnectionString(proveedorBd)
            ?? throw new InvalidOperationException(
                $"No hay ninguna cadena de conexión '{proveedorBd}' en ConnectionStrings (revisa Database:Proveedor).");

        services.AddDbContext<DocFlowDbContext>(options => options.UseSqlServer(connectionString));

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        // Proveedor LLM por perfiles con nombre (Llm:Perfiles:*). El activo se elige con
        // Llm:Proveedor — vía appsettings, variable de entorno Llm__Proveedor o argumento --llm
        var proveedor = configuration["Llm:Proveedor"] ?? "Groq";
        var perfil = configuration.GetSection($"Llm:Perfiles:{proveedor}");
        if (!perfil.Exists())
        {
            var disponibles = configuration.GetSection("Llm:Perfiles").GetChildren().Select(s => s.Key);
            throw new InvalidOperationException(
                $"Perfil LLM '{proveedor}' no definido. Disponibles: {string.Join(", ", disponibles)}.");
        }
        services.Configure<LlmOptions>(perfil);

        services.AddSingleton<IPdfToImageConverter, PdfiumPdfToImageConverter>();
        services.AddSingleton<IImagenNormalizer, Imagen.SkiaImagenNormalizer>();
        services.AddSingleton<IDocumentStorage, FileSystemDocumentStorage>();
        services.AddScoped<IFacturaStagingRepository, FacturaStagingRepository>();
        services.AddScoped<IFacturaConsultaService, FacturaConsultaService>();
        services.AddScoped<IConsultaSqlEjecutor>(_ => new DapperConsultaSqlEjecutor(connectionString));

        services.AddHttpClient<ILlmClient, OpenAiCompatibleLlmClient>(client =>
        {
            // Extracciones multimodal con varias páginas pueden tardar (más aún en local); margen holgado
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        return services;
    }
}
