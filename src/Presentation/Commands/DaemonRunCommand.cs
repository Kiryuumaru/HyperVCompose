using Application.Configuration.Extensions;
using ApplicationBuilderHelpers;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Infrastructure.Serilog;
using Infrastructure.SQLite.LocalStore;
using Microsoft.Extensions.Logging;

namespace Presentation.Commands;

[Command("daemon run", Description = "Daemon run command.")]
public class DaemonRunCommand : MainCommand
{
    public override async ValueTask Run(ApplicationHostBuilder<WebApplicationBuilder> appBuilder, CancellationToken cancellationToken)
    {
        await base.Run(appBuilder, cancellationToken);

        appBuilder.Configuration.SetMakeFileLogs(true);

        try
        {
            await appBuilder.Build().Run(cancellationToken);
        }
        catch (OperationCanceledException) { }
    }
}
