using AbsolutePathHelpers;
using Application;
using Application.Common;
using CliWrap.EventStream;
using CommandLine;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact.Reader;
using Serilog.Parsing;
using System.Globalization;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace Presentation.Common;

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

    private static async Task PrepareSvc(CancellationToken cancellationToken)
    {
        var winswExecPath = AbsolutePath.Create(Environment.CurrentDirectory) / "winsw.exe";
        if (!File.Exists(winswExecPath))
        {
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
            await winswDownloadedExecPath.CopyTo(winswExecPath, cancellationToken);
        }

        var config = """
            <service>
              <id>hyperv-composer</id>
              <name>Hyper-V Composer</name>
              <description>Hyper-V Composer API Service for managing Hyper-V VM instances</description>
              <executable>%BASE%\HyperVCompose.exe</executable>
              <arguments>run</arguments>
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

    private static LogEvent FromLogEvent(LogEvent baseLogEvent, string text, params (string Key, object Value)[] properties)
    {
        var props = baseLogEvent.Properties
            .Where(i => i.Key != "EventGuid")
            .Select(i => new LogEventProperty(i.Key, i.Value))
            .ToList();
        props.Add(new LogEventProperty("EventGuid", new ScalarValue(Guid.NewGuid())));
        foreach (var (Key, Value) in properties)
        {
            props.Add(new LogEventProperty(Key, new ScalarValue(Value)));
        }
        return new LogEvent(baseLogEvent.Timestamp, LogEventLevel.Information, null, new MessageTemplateParser().Parse(text), props);
    }

    public static async Task Logs(int tail, bool follow, CancellationToken cancellationToken)
    {
        CancellationTokenSource? logFileCts = null;
        Guid lastLog = Guid.Empty;
        void printLogEvent(LogEvent logEvent)
        {
            try
            {
                try
                {
                    if (bool.Parse(logEvent.Properties["IsHeadLog"].Cast<ScalarValue>().Value?.ToString()!))
                    {
                        var runtimeGuid = Guid.Parse(logEvent.Properties["RuntimeGuid"].Cast<ScalarValue>().Value?.ToString()!);
                        Log.Write(FromLogEvent(logEvent, "===================================================="));
                        Log.Write(FromLogEvent(logEvent, " Service started: {timestamp}", ("timestamp", logEvent.Timestamp)));
                        Log.Write(FromLogEvent(logEvent, " Runtime ID: {runtimeGuid}", ("runtimeGuid", runtimeGuid)));
                        Log.Write(FromLogEvent(logEvent, "===================================================="));
                    }
                }
                catch { }
                try
                {
                    var lastLog = Guid.Parse(logEvent.Properties["EventGuid"].Cast<ScalarValue>().Value?.ToString()!);
                }
                catch { }
                Log.Write(logEvent);
            }
            catch { }
        }
        void printLogEventStr(string? logEventStr)
        {
            if (string.IsNullOrWhiteSpace(logEventStr))
            {
                return;
            }
            try
            {
                printLogEvent(LogEventReader.ReadFromString(logEventStr));
            }
            catch { }
        }

        foreach (var logEvent in await GetLogEvents(tail, cancellationToken))
        {
            printLogEvent(logEvent);
        }

        if (!follow)
        {
            return;
        }

        bool hasPrintedTail = false;
        await LatestFileListener(async logFile =>
        {
            logFileCts?.Cancel();
            logFileCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var ct = logFileCts.Token;
            try
            {
                using var fileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var streamReader = new StreamReader(fileStream);

                if (!hasPrintedTail)
                {
                    try
                    {
                        while (!ct.IsCancellationRequested)
                        {
                            var line = await streamReader.ReadLineAsync(ct.WithTimeout(TimeSpan.FromMilliseconds(500)));
                            try
                            {
                                var logEventTail = LogEventReader.ReadFromString(line!);
                                var logTail = Guid.Parse(logEventTail.Properties["EventGuid"].Cast<ScalarValue>().Value?.ToString()!);
                                if (logTail == lastLog)
                                {
                                    break;
                                }
                            }
                            catch
                            {
                                break;
                            }
                        }
                    }
                    catch { }
                    hasPrintedTail = true;
                }

                while (!ct.IsCancellationRequested)
                {
                    printLogEventStr(await streamReader.ReadLineAsync(ct));
                }
            }
            catch { }
        }, cancellationToken);
    }

    private static async Task<List<LogEvent>> GetLogEvents(int count, CancellationToken cancellationToken)
    {
        List<LogEvent> logEvents = [];

        List<AbsolutePath> scannedLogFiles = [];
        int printedLines = 0;
        while (true)
        {
            if (count <= printedLines)
            {
                break;
            }

            var latestLogFile = await GetLatestLogFile([.. scannedLogFiles], cancellationToken);

            if (latestLogFile == null)
            {
                break;
            }

            scannedLogFiles.Add(latestLogFile);

            using var fileStream = new FileStream(latestLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var streamReader = new StreamReader(fileStream);

            foreach (var line in (await streamReader.ReadToEndAsync(cancellationToken)).Split(Environment.NewLine).Reverse())
            {
                if (count <= printedLines)
                {
                    break;
                }

                try
                {
                    var logEvent = LogEventReader.ReadFromString(line);
                    logEvents.Add(logEvent);
                    printedLines++;
                }
                catch { }
            }
        }

        return logEvents.ToArray().Reverse().ToList();
    }

    private static async Task LatestFileListener(Action<AbsolutePath> onLogfileChanged, CancellationToken cancellationToken)
    {
        AbsolutePath? logFile = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var latestLogFile = await GetLatestLogFile([], cancellationToken);
            if (latestLogFile != null && logFile != latestLogFile)
            {
                onLogfileChanged(latestLogFile);
                logFile = latestLogFile;
            }
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    private static Task<AbsolutePath?> GetLatestLogFile(AbsolutePath[] skipLogFiles, CancellationToken cancellationToken)
    {
        static (string? LogStr, DateTime LogDateTime) GetLogTime(AbsolutePath logPath)
        {
            var logFileName = logPath.Name;
            if (!logFileName.StartsWith("log-") || !logFileName.EndsWith(".jsonl"))
            {
                return (default, default);
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
            return (currentLogStr, currentLog);
        }
        return Task.Run(() =>
        {
            (string? LogStr, DateTime LogDateTime) latestLogTime = (default, default);
            foreach (var logFile in (Defaults.DataPath / "logs").GetFiles())
            {
                try
                {
                    var currentLogTime = GetLogTime(logFile);
                    bool skip = false;
                    foreach (var skipLogFile in skipLogFiles)
                    {
                        if (GetLogTime(skipLogFile).LogStr == currentLogTime.LogStr)
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (skip)
                    {
                        continue;
                    }
                    if (latestLogTime.LogStr == null || latestLogTime.LogDateTime < currentLogTime.LogDateTime)
                    {
                        latestLogTime = currentLogTime;
                        continue;
                    }
                }
                catch { }
            }
            if (latestLogTime.LogStr == null)
            {
                return null;
            }
            return AbsolutePath.Create(Defaults.DataPath / "logs" / $"log-{latestLogTime.LogStr}.jsonl");
        }, cancellationToken);
    }
}
