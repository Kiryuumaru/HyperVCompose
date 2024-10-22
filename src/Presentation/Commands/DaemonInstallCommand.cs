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
using Microsoft.Extensions.Hosting;

namespace Presentation.Commands;

[Command("daemon install", Description = "Daemon install command.")]
public class DaemonInstallCommand : MainCommand
{
    [CommandOption("username", 'u', Description = "Username of the service account.")]
    public string? Username { get; set; }

    [CommandOption("password", 'p', Description = "Password of the service account.")]
    public string? Password { get; set; }

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        var appBuilder = CreateBuilder();
        var cancellationToken = console.RegisterCancellationHandler();

        var appHost = appBuilder.Build();

        var serviceManager = appHost.Host.Services.GetRequiredService<ClientManager>();

        try
        {
            await serviceManager.Install(Username, Password, cancellationToken);
        }
        catch (OperationCanceledException) { }
    }
}
