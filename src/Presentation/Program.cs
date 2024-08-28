using AbsolutePathHelpers;
using Application;
using Application.Common;
using ApplicationBuilderHelpers;
using CliWrap.EventStream;
using CommandLine;
using CommandLine.Text;
using Infrastructure.SQLite;
using Presentation;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;

return await Parser.Default.ParseArguments<DefaultOption, ServiceOptions, LogsOptions>(args)
    .MapResult(
        (DefaultOption opts) =>
        {
            ApplicationDependencyBuilder.FromBuilder(WebApplication.CreateBuilder(args))
                .Add<BasePresentation>()
                .Add<SQLiteApplication>()
                .Run();
            return Task.FromResult(0);
        },
        async (ServiceOptions opts) =>
        {
            SetLogger();
            var ct = SetCancellableConsole();
            if (opts.Install)
            {
                await ServiceManager.InstallAsService(ct);
            }
            else if (opts.Uninstall)
            {
                await ServiceManager.UninstallAsService(ct);
            }
            return 0;
        },
        async (LogsOptions opts) =>
        {
            SetLogger();
            var ct = SetCancellableConsole();
            await ServiceManager.Logs(opts.Tail, opts.Follow, ct);
            return 0;
        },
        errs => Task.FromResult(-1));

static void SetLogger()
{
    Log.Logger = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .CreateLogger();
}

static CancellationToken SetCancellableConsole()
{
    CancellationTokenSource cts = new();
    Console.CancelKeyPress += (s, e) =>
    {
        cts.Cancel();
    };
    return cts.Token;
}

[Verb("default", true, HelpText = "Run application")]
class DefaultOption
{
}

[Verb("service", HelpText = "Service manager")]
class ServiceOptions
{
    [Option("install", Required = false, HelpText = "Install service.")]
    public bool Install { get; set; }

    [Option("uninstall", Required = false, HelpText = "Uninstall service.")]
    public bool Uninstall { get; set; }
}

[Verb("logs", HelpText = "Get logs.")]
class LogsOptions
{
    [Option('t', "tail", Required = false, HelpText = "Log lines print.", Default = 10)]
    public int Tail { get; set; }

    [Option('f', "follow", Required = false, HelpText = "Follows logs.")]
    public bool Follow { get; set; }
}
