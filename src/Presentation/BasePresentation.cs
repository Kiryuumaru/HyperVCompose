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
using Microsoft.Extensions.Hosting;
using Presentation.Common;
using Presentation.Logger.Enrichers;
using Presentation.Logger.Common;

namespace Presentation;

internal class BasePresentation : BaseApplication
{
    public override void AddConfiguration(ApplicationDependencyBuilder builder, IConfiguration configuration)
    {
        base.AddConfiguration(builder, configuration);

        (configuration as ConfigurationManager)!.AddEnvironmentVariables();

        (builder.Builder as WebApplicationBuilder)!.Host.UseSerilog((context, loggerConfiguration) => LoggerBuilder.Configure(loggerConfiguration, configuration));

        Log.Logger = LoggerBuilder.Configure(new LoggerConfiguration(), configuration).CreateLogger();
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

    public override void RunPreparation(ApplicationDependencyBuilder builder)
    {
        base.RunPreparation(builder);

        Log.Information("Application starting");
    }
}
