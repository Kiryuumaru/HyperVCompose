using Presentation.Logger.Abstractions;

namespace Presentation.Logger.Common.LogEventPropertyTypes;

internal class StringPropertyParser : LogEventPropertyParser<string>
{
    public static StringPropertyParser Default { get; } = new();

    public override object? Parse(string? dataStr)
    {
        return dataStr;
    }
}
