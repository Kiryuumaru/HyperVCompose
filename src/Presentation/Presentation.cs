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
using Microsoft.Extensions.Options;
using Presentation.Services;

namespace Presentation;

internal class Presentation : Application.Application
{
    public override void AddConfiguration(ApplicationHostBuilder applicationBuilder, IConfiguration configuration)
    {
        base.AddConfiguration(applicationBuilder, configuration);

        (configuration as ConfigurationManager)!.AddEnvironmentVariables();
    }

    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddScoped<ClientManager>();

        services.AddHttpClient(Options.DefaultName, client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", Defaults.AppNamePascalCase);
        });

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

    public override void AddMiddlewares(ApplicationHost applicationHost, IHost host)
    {
        base.AddMiddlewares(applicationHost, host);

        (host as IApplicationBuilder)!.UseSwagger();
        (host as IApplicationBuilder)!.UseSwaggerUI();

        (host as IApplicationBuilder)!.UseHttpsRedirection();
    }

    public override void AddMappings(ApplicationHost applicationHost, IHost host)
    {
        base.AddMappings(applicationHost, host);

        (host as WebApplication)!.UseHttpsRedirection();
        (host as WebApplication)!.UseAuthorization();
        (host as WebApplication)!.MapControllers();
    }

    public override void RunPreparation(ApplicationHost applicationHost)
    {
        base.RunPreparation(applicationHost);

        Log.Information("Application starting");
    }
}
