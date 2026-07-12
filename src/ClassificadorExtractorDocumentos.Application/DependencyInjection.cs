using ClassificadorExtractorDocumentos.Application.Consultor;
using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Application.Ingesta;
using ClassificadorExtractorDocumentos.Application.Validacion;
using ClassificadorExtractorDocumentos.Domain.Validacion;
using ClassificadorExtractorDocumentos.Domain.Validacion.Reglas;
using Microsoft.Extensions.DependencyInjection;

namespace ClassificadorExtractorDocumentos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IngestaDocumentoService>();
        services.AddScoped<ExtractorAgent>();
        services.AddScoped<ValidadorAgent>();
        services.AddScoped<IngestaOrquestador>();
        services.AddScoped<ConsultorAgent>();
        services.AddSingleton(TimeProvider.System);

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
