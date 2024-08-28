using Application;
using ApplicationBuilderHelpers;
using Microsoft.OpenApi.Models;
using System.Reflection;
using Serilog;
using Serilog.Core;
using AbsolutePathHelpers;
using System.Xml.Linq;
using Serilog.Formatting.Compact;
using Serilog.Events;
using Newtonsoft.Json.Linq;
using Application.Common;

namespace Presentation;

internal class BasePresentation : BaseApplication
{
    class LogGuidEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent evt, ILogEventPropertyFactory _)
        {
            evt.AddOrUpdateProperty(new LogEventProperty("EventGuid", new ScalarValue(Guid.NewGuid())));
        }
    }

    private static LoggerConfiguration ConfigureLogger(LoggerConfiguration loggerConfiguration, IConfiguration configuration)
    {
        loggerConfiguration = loggerConfiguration
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.With(new LogGuidEnricher())
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug);

        if (configuration.GetVarRefValueOrDefault("MAKE_LOGS", "no").Equals("no", StringComparison.InvariantCultureIgnoreCase))
        {
            loggerConfiguration = loggerConfiguration
                .WriteTo.File(
                    formatter: new CompactJsonFormatter(),
                    path: Defaults.DataPath / "logs" / "log-.jsonl",
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    rollingInterval: RollingInterval.Hour);
        }

        return loggerConfiguration;
    }

    public override void AddConfiguration(ApplicationDependencyBuilder builder, IConfiguration configuration)
    {
        base.AddConfiguration(builder, configuration);

        Log.Logger = ConfigureLogger(new LoggerConfiguration(), configuration).CreateLogger();

        (builder.Builder as WebApplicationBuilder)!.Host.UseSerilog((context, loggerConfiguration) => ConfigureLogger(loggerConfiguration, configuration));

        (configuration as ConfigurationManager)!.AddEnvironmentVariables();
    }

    public override void AddServices(ApplicationDependencyBuilder builder, IServiceCollection services)
    {
        base.AddServices(builder, services);

        services.AddMvc();
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "HyperV Compose",
                Description = "Hyper-V composer API",
                Contact = new OpenApiContact
                {
                    Name = "Kiryuumaru",
                    Url = new Uri("https://github.com/Kiryuumaru")
                }
            });

            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
        });
    }

    public override void AddMiddlewares(ApplicationDependencyBuilder builder, IHost host)
    {
        base.AddMiddlewares(builder, host);

        (host as IApplicationBuilder)!.UseSerilogRequestLogging();

        (host as IApplicationBuilder)!.UseSwagger();
        (host as IApplicationBuilder)!.UseSwaggerUI();

        (host as IApplicationBuilder)!.UseHttpsRedirection();
    }

    public override void AddMappings(ApplicationDependencyBuilder builder, IHost host)
    {
        base.AddMappings(builder, host);

        (host as WebApplication)!.UseHttpsRedirection();
        (host as WebApplication)!.UseAuthorization();
        (host as WebApplication)!.MapControllers();
        (host as WebApplication)!.UseSerilogRequestLogging();
    }
}
