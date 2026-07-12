using ClassificadorExtractorDocumentos.Application.Consultor;
using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Application.Ingesta;
using ClassificadorExtractorDocumentos.Application.Ingesta.Maf;
using ClassificadorExtractorDocumentos.Application.Validacion;
using ClassificadorExtractorDocumentos.Domain.Validacion;
using ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClassificadorExtractorDocumentos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IngestaDocumentoService>();
        services.AddScoped<ExtractorAgent>();
        services.AddScoped<ValidadorAgent>();
        services.AddScoped<ConsultorAgent>();
        services.AddSingleton(TimeProvider.System);

        // Pipeline de ingesta: motor conmutable (SPEC §7). "Maf" (Etapa 2, por defecto) o "Manual" (plan B).
        // Se elige con Pipeline:Motor en appsettings / env / argumento.
        services.AddScoped<IngestaOrquestador>();
        services.AddScoped<MafIngestaOrquestador>();

        var motor = configuration["Pipeline:Motor"] ?? "Maf";
        if (motor.Equals("Manual", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IIngestaPipeline>(sp => sp.GetRequiredService<IngestaOrquestador>());
        }
        else
        {
            services.AddScoped<IIngestaPipeline>(sp => sp.GetRequiredService<MafIngestaOrquestador>());
        }

        // Las 9 reglas del Validador (SPEC §2.3), patrón Strategy
        services.AddSingleton<IReglaValidacion, ReglaCuadreLineas>();
        services.AddSingleton<IReglaValidacion, ReglaCuadreTotal>();
        services.AddSingleton<IReglaValidacion, ReglaIvaCoherente>();
        services.AddSingleton<IReglaValidacion, ReglaReverseCharge>();
        services.AddSingleton<IReglaValidacion, ReglaNifFormato>();
        services.AddSingleton<IReglaValidacion, ReglaCamposObligatorios>();
        services.AddSingleton<IReglaValidacion, ReglaConfidenceMinima>();
        services.AddSingleton<IReglaValidacion, ReglaFechaRazonable>();
        services.AddSingleton<IReglaValidacion, ReglaDuplicado>();

        return services;
    }
}
