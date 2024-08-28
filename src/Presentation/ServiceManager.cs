using AbsolutePathHelpers;
using Application;
using Application.Common;
using CliWrap.EventStream;
using Serilog;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Presentation;

internal static class ServiceManager
{
    public static async Task InstallAsService(CancellationToken cancellationToken)
    {
        Log.Information("Installing service...");

        await PrepareSvc(cancellationToken);

        var winswExecPath = AbsolutePath.Create(Environment.CurrentDirectory) / "winsw.exe";
        var serviceConfig = AbsolutePath.Create(Environment.CurrentDirectory) / "svc.xml";

        try
        {
            await Cli.RunOnce($"{winswExecPath} stop {serviceConfig} --force", stoppingToken: cancellationToken);
            await Cli.RunOnce($"{winswExecPath} uninstall {serviceConfig}", stoppingToken: cancellationToken);
        }
        catch { }
        await Cli.RunOnce($"{winswExecPath} install {serviceConfig}", stoppingToken: cancellationToken);
        await Cli.RunOnce($"{winswExecPath} start {serviceConfig}", stoppingToken: cancellationToken);

        Log.Information("Installing service done");
    }

    public static async Task UninstallAsService(CancellationToken cancellationToken)
    {
        Log.Information("Uninstalling service...");

        await PrepareSvc(cancellationToken);

        var winswExecPath = AbsolutePath.Create(Environment.CurrentDirectory) / "winsw.exe";
        var serviceConfig = AbsolutePath.Create(Environment.CurrentDirectory) / "svc.xml";

        try
        {
            await Cli.RunOnce($"{winswExecPath} stop {serviceConfig} --force", stoppingToken: cancellationToken);
        }
        catch { }
        await Cli.RunOnce($"{winswExecPath} uninstall {serviceConfig}", stoppingToken: cancellationToken);

        Log.Information("Uninstalling service done");
    }

    public static async Task PrepareSvc(CancellationToken cancellationToken)
    {
        await DownloadWinsw(cancellationToken);
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

    public static async Task DownloadWinsw(CancellationToken cancellationToken)
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
        await winswZipPath.UnZipTo(winswZipExtractPath, cancellationToken);
        await winswDownloadedExecPath.Copy(winswExecPath, cancellationToken);
    }

    public static async Task Logs(int tail, bool follow, CancellationToken cancellationToken)
    {
        CancellationTokenSource? logFileCts = null;
        Guid lastLog = Guid.Empty;
        await LatestFileListener(async logFile =>
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
                        Log.Write(logEvent);
                    }
                    catch { }
                }
            }
            catch { }
        }, cancellationToken);
    }

    public static async Task LatestFileListener(Action<AbsolutePath> onLogfileChanged, CancellationToken cancellationToken)
    {
        AbsolutePath? logFile = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var latestLogFile = await GetLatestLogFile(cancellationToken);
            if (latestLogFile != null && logFile != latestLogFile)
            {
                onLogfileChanged(latestLogFile);
                logFile = latestLogFile;
            }
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    public static Task<AbsolutePath?> GetLatestLogFile(CancellationToken cancellationToken)
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
}
