using AbsolutePathHelpers;
using Application;
using ApplicationBuilderHelpers;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Infrastructure.Serilog;
using Infrastructure.SQLite.LocalStore;
using Microsoft.Extensions.Logging;
using Serilog;
using Application.Common;
using System.Runtime.InteropServices;
using Presentation.Services;

namespace Presentation.Commands;

[Command("daemon stop", Description = "Daemon stop command.")]
public class DaemonStopCommand : MainCommand
{
    public override async ValueTask Run(ApplicationHostBuilder<WebApplicationBuilder> appBuilder, CancellationToken cancellationToken)
    {
        await base.Run(appBuilder, cancellationToken);

        var appHost = appBuilder.Build();

        var serviceManager = appHost.Host.Services.GetRequiredService<ServiceManager>();

        await serviceManager.Stop(cancellationToken);
    }
}
