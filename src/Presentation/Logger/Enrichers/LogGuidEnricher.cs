using Application;
using Serilog.Core;
using Serilog.Events;

namespace Presentation.Logger.Enrichers;

internal class LogGuidEnricher : ILogEventEnricher
{
    private static bool HasHeadRuntimeLogs { get; set; } = false;

    public void Enrich(LogEvent evt, ILogEventPropertyFactory _)
    {
        evt.AddOrUpdateProperty(new LogEventProperty("EventGuid", new ScalarValue(Guid.NewGuid())));
        evt.AddOrUpdateProperty(new LogEventProperty("RuntimeGuid", new ScalarValue(Defaults.RuntimeGuid)));
        if (!HasHeadRuntimeLogs)
        {
            evt.AddOrUpdateProperty(new LogEventProperty("IsHeadLog", new ScalarValue(true)));
            HasHeadRuntimeLogs = true;
        }
    }
}