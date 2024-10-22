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
using Application.Logger.Interfaces;

namespace Presentation.Commands;

[Command("daemon uninstall", Description = "Daemon uninstall command.")]
public class DaemonUninstallCommand : MainCommand
{
    public override async ValueTask ExecuteAsync(IConsole console)
    {
        var appBuilder = CreateBuilder();
        var cancellationToken = console.RegisterCancellationHandler();

        var appHost = appBuilder.Build();

        var serviceManager = appHost.Host.Services.GetRequiredService<ClientManager>();

        try
        {
            await serviceManager.Uninstall(cancellationToken);
        }
        catch (OperationCanceledException) { }
    }
}
