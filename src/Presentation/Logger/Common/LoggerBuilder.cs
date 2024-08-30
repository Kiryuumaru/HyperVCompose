using Application;
using ApplicationBuilderHelpers;
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
using Serilog.Sinks.SystemConsole.Themes;
using Application.Configuration.Extensions;

namespace Presentation.Logger.Common;

internal static class LoggerBuilder
{
    public static SystemConsoleTheme Theme()
    {
        return new SystemConsoleTheme(new Dictionary<ConsoleThemeStyle, SystemConsoleThemeStyle>()
        {
            [ConsoleThemeStyle.Text] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.White },
            [ConsoleThemeStyle.SecondaryText] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.Gray },
            [ConsoleThemeStyle.TertiaryText] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.DarkGray },
            [ConsoleThemeStyle.Invalid] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.Yellow },
            [ConsoleThemeStyle.Null] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.Blue },
            [ConsoleThemeStyle.Name] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.Gray },
            [ConsoleThemeStyle.String] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.Cyan },
            [ConsoleThemeStyle.Number] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.Magenta },
            [ConsoleThemeStyle.Boolean] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.Blue },
            [ConsoleThemeStyle.Scalar] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.Green },
            [ConsoleThemeStyle.LevelVerbose] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.Gray },
            [ConsoleThemeStyle.LevelDebug] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.White },
            [ConsoleThemeStyle.LevelInformation] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.Green },
            [ConsoleThemeStyle.LevelWarning] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.Black, Background = ConsoleColor.Yellow },
            [ConsoleThemeStyle.LevelError] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.Black, Background = ConsoleColor.Red },
            [ConsoleThemeStyle.LevelFatal] = new SystemConsoleThemeStyle { Foreground = ConsoleColor.White, Background = ConsoleColor.Magenta }
        });
    }

    public static LoggerConfiguration Configure(LoggerConfiguration loggerConfiguration, IConfiguration configuration)
    {
        loggerConfiguration = loggerConfiguration
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.With(new LogGuidEnricher(configuration))
            .WriteTo.Console(
                outputTemplate: "{Timestamp:u} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Debug,
                theme: Theme());
        
        if (configuration?.GetVarRefValueOrDefault("MAKE_LOGS", "no").Equals("svc", StringComparison.InvariantCultureIgnoreCase) ?? false)
        {
            loggerConfiguration = loggerConfiguration
                .MinimumLevel.Verbose()
                .WriteTo.File(
                    formatter: new CompactJsonFormatter(),
                    path: configuration.GetDataPath() / "logs" / "log-.jsonl",
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    rollingInterval: RollingInterval.Hour);
        }

        return loggerConfiguration;
    }
}
