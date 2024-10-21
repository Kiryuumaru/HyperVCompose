using Application.Configuration.Extensions;
using ApplicationBuilderHelpers;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Infrastructure.Serilog;
using Infrastructure.SQLite.LocalStore;
using Microsoft.Extensions.Logging;
using Presentation.Services;

namespace Presentation.Commands;

[Command("update", Description = "Update client.")]
public class UpdateCommand : MainCommand
{
    public override async ValueTask Run(ApplicationHostBuilder<WebApplicationBuilder> appBuilder, CancellationToken cancellationToken)
    {
        await base.Run(appBuilder, cancellationToken);

        var appHost = appBuilder.Build();

        var serviceManager = appHost.Host.Services.GetRequiredService<ServiceManager>();

        await serviceManager.UpdateClient(cancellationToken);
    }
}
