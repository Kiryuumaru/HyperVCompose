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

namespace Presentation.Logger.Common;

internal static class LoggerBuilder
{
    public static SystemConsoleTheme Theme()
    {
        return new SystemConsoleTheme(new Dictionary<ConsoleThemeStyle, SystemConsoleThemeStyle>()
        {
            {
                ConsoleThemeStyle.String, new SystemConsoleThemeStyle
                {
                    Foreground = ConsoleColor.Green,
                }
            },
            {
                ConsoleThemeStyle.Number, new SystemConsoleThemeStyle
                {
                    Foreground = ConsoleColor.Cyan,
                }
            },
            {
                ConsoleThemeStyle.Boolean, new SystemConsoleThemeStyle
                {
                    Foreground = ConsoleColor.Red,
                }
            },
            {
                ConsoleThemeStyle.LevelInformation, new SystemConsoleThemeStyle
                {
                    Foreground = ConsoleColor.Black,
                    Background = ConsoleColor.White
                }
            },
            {
                ConsoleThemeStyle.SecondaryText, new SystemConsoleThemeStyle
                {
                    Foreground = ConsoleColor.White
                }
            },
            {
                ConsoleThemeStyle.Null, new SystemConsoleThemeStyle
                {
                    Foreground = ConsoleColor.Black,
                    Background = ConsoleColor.Yellow,
                }
            },
            {
                ConsoleThemeStyle.LevelError, new SystemConsoleThemeStyle
                {
                    Foreground = ConsoleColor.White,
                    Background = ConsoleColor.Red,
                }
            },
            {
                ConsoleThemeStyle.LevelFatal, new SystemConsoleThemeStyle
                {
                    Foreground = ConsoleColor.Black, Background = ConsoleColor.Cyan,
                }
            },
            {
                ConsoleThemeStyle.LevelWarning, new SystemConsoleThemeStyle
                {
                    Foreground = ConsoleColor.Magenta,
                    Background = ConsoleColor.Yellow,
                }
            },
            {
                ConsoleThemeStyle.LevelVerbose, new SystemConsoleThemeStyle
                {
                    Foreground = ConsoleColor.White,
                    Background = ConsoleColor.Green,
                }
            },
            {
                ConsoleThemeStyle.Name, new SystemConsoleThemeStyle
                {
                    Foreground = ConsoleColor.Black,
                    Background = ConsoleColor.Yellow,
                }
            }

        });
    }

    public static LoggerConfiguration Configure(LoggerConfiguration loggerConfiguration, IConfiguration? configuration = null)
    {
        loggerConfiguration = loggerConfiguration
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.With(new LogGuidEnricher())
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
                    path: Defaults.DataPath / "logs" / "log-.jsonl",
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    rollingInterval: RollingInterval.Day);
        }

        return loggerConfiguration;
    }
}
