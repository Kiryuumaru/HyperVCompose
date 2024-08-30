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

namespace Presentation.Logger;

internal class LogsExtension
{
    public static async Task Logs(int tail, bool follow, CancellationToken cancellationToken)
    {
        CancellationTokenSource? logFileCts = null;
        Guid lastLog = Guid.Empty;
        void printLogEvent(LogEvent logEvent)
        {
            if (logEvent.Properties.TryGetValue("IsHeadLog", out var isHeadLogProp) &&
                bool.TryParse(isHeadLogProp.Cast<ScalarValue>().Value?.ToString()!, out bool isHeadLog) &&
                isHeadLog &&
                logEvent.Properties.TryGetValue("RuntimeGuid", out var runtimeGuidProp) &&
                Guid.TryParse(runtimeGuidProp.Cast<ScalarValue>().Value?.ToString()!, out var runtimeGuid))
            {
                Log.Write(FromLogEvent(logEvent, "===================================================="));
                Log.Write(FromLogEvent(logEvent, " Service started: {timestamp}", ("timestamp", logEvent.Timestamp)));
                Log.Write(FromLogEvent(logEvent, " Runtime ID: {runtimeGuid}", ("runtimeGuid", runtimeGuid)));
                Log.Write(FromLogEvent(logEvent, "===================================================="));
            }
            if (logEvent.Properties.TryGetValue("EventGuid", out var eventGuidProp) &&
                Guid.TryParse(eventGuidProp.Cast<ScalarValue>().Value?.ToString()!, out var eventGuid))
            {
                lastLog = eventGuid;
            }
            Log.Write(logEvent);
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

                var wh = new AutoResetEvent(false);
                var fsw = new FileSystemWatcher(logFile)
                {
                    Filter = logFile,
                    EnableRaisingEvents = true
                };
                fsw.Changed += (s, e) => wh.Set();

                while (!ct.IsCancellationRequested)
                {
                    string? line = await streamReader.ReadLineAsync(ct);
                    if (line != null)
                    {
                        printLogEventStr(line);
                    }
                    else
                    {
                        wh.WaitOne(1000);
                    }
                }
            }
            catch { }
        }, cancellationToken);
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
