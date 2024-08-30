using AbsolutePathHelpers;
using Application;
using Application.Common;
using ApplicationBuilderHelpers;
using CliWrap.EventStream;
using CommandLine;
using CommandLine.Text;
using Infrastructure.SQLite;
using Infrastructure.SQLite.LocalStore;
using Microsoft.Extensions.Configuration;
using Presentation;
using Presentation.Common;
using Presentation.Logger.Common;
using Presentation.Logger.Extensions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

var parserResult = new Parser(with =>
    {
        with.CaseInsensitiveEnumValues = true;
        with.CaseSensitive = false;
        with.IgnoreUnknownArguments = false;
    })
    .ParseArguments<RunOption, ServiceOptions, LogsOptions>(args);

return await parserResult
    .WithNotParsed(_ => DisplayHelp(parserResult))
    .MapResult(
        (RunOption opts) =>
        {
            if (Validate(parserResult, opts))
            {
                var builder = WebApplication.CreateBuilder(args);
                if (opts.AsService)
                {
                    builder.Configuration["MAKE_LOGS"] = "svc";
                }
                ApplicationDependencyBuilder.FromBuilder(builder)
                    .Add<BasePresentation>()
                    .Add<SQLiteLocalStoreInfrastructure>()
                    .Run();
                return Task.FromResult(0);
            }
            return Task.FromResult(-1);
        },
        async (ServiceOptions opts) =>
        {
            if (Validate(parserResult, opts))
            {
                var (_, ct) = SetupCli(opts.LogLevel);
                try
                {
                    if (opts.Install)
                    {
                        await ServiceExtension.InstallAsService(ct);
                    }
                    else if (opts.Uninstall)
                    {
                        await ServiceExtension.UninstallAsService(ct);
                    }
                }
                catch (OperationCanceledException) { }
                return 0;
            }
            return -1;
        },
        async (LogsOptions opts) =>
        {
            if (Validate(parserResult, opts))
            {
                var (configuration, ct) = SetupCli(opts.LogLevel);
                try
                {
                    await LogsExtension.Logs(configuration, opts.Tail, opts.Follow, ct);
                }
                catch (OperationCanceledException) { }
                return 0;
            }
            return -1;
        },
        errs => Task.FromResult(-1));

static (IConfiguration Configuration, CancellationToken CancellationToken) SetupCli(LogEventLevel logEventLevel)
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Environment.CurrentDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();
    Log.Logger = LoggerBuilder.Configure(new LoggerConfiguration(), configuration)
        .MinimumLevel.Is(logEventLevel)
        .CreateLogger();
    CancellationTokenSource cts = new();
    Console.CancelKeyPress += (s, e) =>
    {
        cts.Cancel();
    };
    return (configuration, cts.Token);
}

void DisplayHelp<T>(ParserResult<T> result)
{
    if (result.Errors.IsVersion())
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
        Console.WriteLine(fileVersionInfo.ProductVersion);
    }
    else
    {
        Console.WriteLine(HelpText.AutoBuild(result, help =>
        {
            help.AddEnumValuesToHelpText = true;
            help.AutoHelp = true;
            help.AutoVersion = true;
            help.AddDashesToOption = true;

            help.AddOptions(result);

            return HelpText.DefaultParsingErrorsHandler(result, help);

        }, e => e));
    }
}

bool Validate<T>(ParserResult<T> parserResult, IArgumentValidation argsToValidate)
{
    try
    {
        argsToValidate.Validate();
        return true;
    }
    catch (ArgumentValidationException ex)
    {
        Console.WriteLine();
        Console.WriteLine("Invalid arguments detected: {0}", ex.Message);
        Console.WriteLine();
        DisplayHelp(parserResult);
        return false;
    }
}

[Verb("run", HelpText = "Run application")]
class RunOption : IArgumentValidation
{
    [Option('s', "as-service", Required = false, HelpText = "Run as service mode.")]
    public bool AsService { get; set; }

    public void Validate()
    {
    }
}

[Verb("service", HelpText = "Service manager")]
class ServiceOptions : IArgumentValidation
{
    [Option("install", Required = false, HelpText = "Install service.")]
    public bool Install { get; set; }

    [Option("uninstall", Required = false, HelpText = "Uninstall service.")]
    public bool Uninstall { get; set; }

    [Option('l', "level", Required = false, HelpText = "Level of logs to show.", Default = LogEventLevel.Information)]
    public LogEventLevel LogLevel { get; set; }

    public void Validate()
    {
        if (!Install && !Uninstall)
        {
            throw new ArgumentValidationException($"No operation selected");
        }
    }
}

[Verb("logs", HelpText = "Get logs.")]
class LogsOptions : IArgumentValidation
{
    [Option('t', "tail", Required = false, HelpText = "Log lines print.", Default = 10)]
    public int Tail { get; set; }

    [Option('f', "follow", Required = false, HelpText = "Follows logs.")]
    public bool Follow { get; set; }

    [Option('l', "level", Required = false, HelpText = "Level of logs to show.", Default = LogEventLevel.Information)]
    public LogEventLevel LogLevel { get; set; }

    public void Validate()
    {
    }
}

class ArgumentValidationException(string message) : Exception(message)
{
}

interface IArgumentValidation
{
    void Validate();
}
