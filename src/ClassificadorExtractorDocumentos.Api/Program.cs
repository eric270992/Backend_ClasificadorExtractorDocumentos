using ClassificadorExtractorDocumentos.Application;
using ClassificadorExtractorDocumentos.Infrastructure;

namespace ClassificadorExtractorDocumentos
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Atajo de línea de comandos: dotnet run -- --llm Local  (equivale a Llm:Proveedor=Local)
            builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
            {
                ["--llm"] = "Llm:Proveedor",
            });

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();
            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(builder.Configuration);

            // CORS para el frontend Angular. En dev el proxy evita CORS, pero esto permite
            // llamar a la API directamente (p. ej. sin proxy) desde los orígenes del front.
            const string CorsFrontend = "FrontendDev";
            var origenesPermitidos = builder.Configuration.GetSection("Cors:Origenes").Get<string[]>()
                ?? ["http://localhost:4200"];
            builder.Services.AddCors(options =>
                options.AddPolicy(CorsFrontend, policy =>
                    policy.WithOrigins(origenesPermitidos).AllowAnyHeader().AllowAnyMethod()));

            var app = builder.Build();

            app.Logger.LogInformation(
                "Proveedor LLM activo: {Proveedor} → {BaseUrl} ({Model})",
                app.Configuration["Llm:Proveedor"] ?? "Groq",
                app.Configuration[$"Llm:Perfiles:{app.Configuration["Llm:Proveedor"] ?? "Groq"}:BaseUrl"],
                app.Configuration[$"Llm:Perfiles:{app.Configuration["Llm:Proveedor"] ?? "Groq"}:Model"]);

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseCors(CorsFrontend);

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
