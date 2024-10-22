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
    public override async ValueTask ExecuteAsync(IConsole console)
    {
        var appBuilder = CreateBuilder();
        var cancellationToken = console.RegisterCancellationHandler();

        var appHost = appBuilder.Build();

        var serviceManager = appHost.Host.Services.GetRequiredService<ClientManager>();

        try
        {
            await serviceManager.Stop(cancellationToken);
        }
        catch (OperationCanceledException) { }
    }
}
