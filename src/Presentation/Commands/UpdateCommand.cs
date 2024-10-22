using Application.Configuration.Extensions;
using Application.Logger.Interfaces;
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
    public override async ValueTask ExecuteAsync(IConsole console)
    {
        var appBuilder = CreateBuilder();
        var cancellationToken = console.RegisterCancellationHandler();

        var appHost = appBuilder.Build();

        var serviceManager = appHost.Host.Services.GetRequiredService<ClientManager>();

        try
        {
            await serviceManager.UpdateClient(cancellationToken);
        }
        catch (OperationCanceledException) { }
    }
}
