using AbsolutePathHelpers;
using Application.Configuration.Extensions;
using ApplicationBuilderHelpers;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Infrastructure.Serilog;
using Infrastructure.SQLite.LocalStore;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using System.Reflection.PortableExecutable;

namespace Presentation.Commands;

[Command(Description = "The main command.")]
public class MainCommand : ICommand
{
    [CommandOption("log-level", 'l', Description = "Level of logs to show.")]
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    [CommandOption("as-json", Description = "Output as json.")]
    public bool AsJson { get; set; } = false;

    [CommandOption("home", Description = "Home directory.", EnvironmentVariable = "HYPERV_COMPOSE_HOME")]
    public string Home { get; set; } = AbsolutePath.Create(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)) / "hvc";

    public ValueTask ExecuteAsync(IConsole console)
    {
        var appBuilder = ApplicationHost.FromBuilder(WebApplication.CreateBuilder())
            .Add<Presentation>()
            .Add<SerilogInfrastructure>()
            .Add<SQLiteLocalStoreInfrastructure>();

        appBuilder.Configuration.SetLoggerLevel(LogLevel);
        appBuilder.Configuration.SetHomePath(GetHome());

        return Run(appBuilder, console.RegisterCancellationHandler());
    }

    public virtual ValueTask Run(ApplicationHostBuilder<WebApplicationBuilder> appBuilder, CancellationToken cancellationToken)
    {
        return default;
    }

    internal AbsolutePath GetHome()
    {
        try
        {
            return AbsolutePath.Create(Home);
        }
        catch
        {
            throw new CommandException($"Invalid home directory \"{Home}\".", 1000);
        }
    }
}
