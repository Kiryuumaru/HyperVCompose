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

CancellationTokenSource cts = new();
CancellationToken ct = cts.Token;
Console.CancelKeyPress += (s, e) =>
{
    cts.Cancel();
};

using var log = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

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
            if (opts.Install)
            {
                await installAsService(ct);
            }
            else if (opts.Uninstall)
            {
                await uninstallAsService(ct);
            }
            return 0;
        },
        async (LogsOptions opts) =>
        {
            await logs(opts.Tail, opts.Follow, ct);
            return 0;
        },
        errs => Task.FromResult(-1));

async Task installAsService(CancellationToken cancellationToken)
{
    log.Information("Installing service...");

    await prepareSvc(cancellationToken);

    var winswExecPath = Environment.CurrentDirectory.Trim('\\') + "\\winsw.exe";
    var serviceConfig = Environment.CurrentDirectory.Trim('\\') + "\\svc.xml";

    try
    {
        await Cli.RunOnce($"{winswExecPath} stop {serviceConfig} --force", stoppingToken: cancellationToken);
        await Cli.RunOnce($"{winswExecPath} uninstall {serviceConfig}", stoppingToken: cancellationToken);
    }
    catch { }
    await Cli.RunOnce($"{winswExecPath} install {serviceConfig}", stoppingToken: cancellationToken);
    await Cli.RunOnce($"{winswExecPath} start {serviceConfig}", stoppingToken: cancellationToken);

    log.Information("Installing service done");
}

async Task uninstallAsService(CancellationToken cancellationToken)
{
    log.Information("Uninstalling service...");

    await prepareSvc(cancellationToken);

    var winswExecPath = Environment.CurrentDirectory.Trim('\\') + "\\winsw.exe";
    var serviceConfig = Environment.CurrentDirectory.Trim('\\') + "\\svc.xml";

    try
    {
        await Cli.RunOnce($"{winswExecPath} stop {serviceConfig} --force", stoppingToken: cancellationToken);
    }
    catch { }
    await Cli.RunOnce($"{winswExecPath} uninstall {serviceConfig}", stoppingToken: cancellationToken);

    log.Information("Uninstalling service done");
}

async Task prepareSvc(CancellationToken cancellationToken)
{
    await downloadWinsw(cancellationToken);
    var config = """
        <service>
          <id>hyperv-composer</id>
          <name>Hyper-V Composer</name>
          <description>Hyper-V Composer API Service for managing Hyper-V VM instances</description>
          <executable>%BASE%\HyperVCompose.exe</executable>
          <log mode="none"></log>
          <startmode>Automatic</startmode>
          <onfailure action="restart" delay="2 sec"/>
          <env name="ASPNETCORE_URLS" value="http://*:23456" />
          <env name="MAKE_LOGS" value="svc" />
        </service>
        """;
    var serviceConfig = AbsolutePath.Create(Environment.CurrentDirectory) / "svc.xml";
    await File.WriteAllTextAsync(serviceConfig, config, cancellationToken);
}

async Task downloadWinsw(CancellationToken cancellationToken)
{
    var winswExecPath = AbsolutePath.Create(Environment.CurrentDirectory) / "winsw.exe";
    if (File.Exists(winswExecPath))
    {
        return;
    }
    string folderName;
    if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
    {
        folderName = "winsw_windows_x64";
    }
    else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
    {
        folderName = "winsw_windows_arm64";
    }
    else
    {
        throw new NotSupportedException();
    }
    string dlUrl = $"https://github.com/Kiryuumaru/winsw-modded/releases/download/build.1/{folderName}.zip";
    var downloadsPath = AbsolutePath.Create(Environment.CurrentDirectory) / "downloads";
    var winswZipPath = downloadsPath / "winsw.zip";
    var winswZipExtractPath = downloadsPath / "winsw";
    var winswDownloadedExecPath = winswZipExtractPath / folderName / "winsw.exe";
    try
    {
        await winswZipPath.Delete(cancellationToken);
    }
    catch { }
    try
    {
        await winswZipExtractPath.Delete(cancellationToken);
    }
    catch { }
    downloadsPath.CreateDirectory();
    winswZipExtractPath.CreateDirectory();
    {
        using var client = new HttpClient();
        using var s = await client.GetStreamAsync(dlUrl, cancellationToken: cancellationToken);
        using var fs = new FileStream(winswZipPath, FileMode.OpenOrCreate);
        await s.CopyToAsync(fs, cancellationToken: cancellationToken);
    }
    ZipFile.ExtractToDirectory(winswZipPath, winswZipExtractPath);
    File.Copy(winswDownloadedExecPath, winswExecPath);
}

async Task logs(int tail, bool follow, CancellationToken cancellationToken)
{
    CancellationTokenSource? logFileCts = null;
    Guid lastLog = Guid.Empty;
    await latestFileListener(async logFile =>
    {
        logFileCts?.Cancel();
        logFileCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            List<string> logsArgs = ["Get-Content", "-Path", logFile];
            if (follow)
            {
                logsArgs.Add("-Wait");
            }
            await foreach (var commandEvent in Cli.RunListen("powershell", [.. logsArgs], stoppingToken: logFileCts.Token))
            {
                string? text = null;
                switch (commandEvent)
                {
                    case StandardOutputCommandEvent outEvent:
                        text = outEvent.Text;
                        break;
                    case StandardErrorCommandEvent errEvent:
                        text = errEvent.Text;
                        break;
                }
                if (text == null)
                {
                    continue;
                }
                try
                {
                    var logEvent = Serilog.Formatting.Compact.Reader.LogEventReader.ReadFromString(text);
                    log.Write(logEvent);
                }
                catch { }
            }
        }
        catch { }
    }, ct);
}

async Task latestFileListener(Action<AbsolutePath> onLogfileChanged, CancellationToken cancellationToken)
{
    AbsolutePath? logFile = null;
    while (!cancellationToken.IsCancellationRequested)
    {
        var latestLogFile = await getLatestLogFile(cancellationToken);
        if (latestLogFile != null && logFile != latestLogFile)
        {
            onLogfileChanged(latestLogFile);
            logFile = latestLogFile;
        }
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    }
}

Task<AbsolutePath?> getLatestLogFile(CancellationToken cancellationToken)
{
    return Task.Run(() =>
    {
        string? latestLogStr = null;
        DateTime latestLog = DateTime.MinValue;
        foreach (var logFile in (Defaults.DataPath / "logs").GetFiles())
        {
            try
            {
                var logFileName = logFile.Name;
                if (!logFileName.StartsWith("log-") || !logFileName.EndsWith(".jsonl"))
                {
                    continue;
                }
                var currentLogStr = logFileName.Replace("log-", "").Replace(".jsonl", "");
                DateTime currentLog = currentLogStr.Length switch
                {
                    12 => DateTime.ParseExact(currentLogStr, "yyyyMMddHHmm", CultureInfo.InvariantCulture),
                    10 => DateTime.ParseExact(currentLogStr, "yyyyMMddHH", CultureInfo.InvariantCulture),
                    8 => DateTime.ParseExact(currentLogStr, "yyyyMMdd", CultureInfo.InvariantCulture),
                    6 => DateTime.ParseExact(currentLogStr, "yyyyMM", CultureInfo.InvariantCulture),
                    _ => throw new Exception()
                };
                if (latestLogStr == null ||
                    latestLog < currentLog)
                {
                    latestLogStr = currentLogStr;
                    latestLog = currentLog;
                    continue;
                }
            }
            catch { }
        }
        if (latestLogStr == null)
        {
            return null;
        }
        return AbsolutePath.Create(Defaults.DataPath / "logs" / $"log-{latestLogStr}.jsonl");
    }, cancellationToken);
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
