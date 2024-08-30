using Presentation.Logger.Abstractions;

namespace Presentation.Logger.Common.LogEventPropertyTypes;

internal class GuidPropertyParser : LogEventPropertyParser<Guid>
{
    public static GuidPropertyParser Default { get; } = new();

    public override object? Parse(string? dataStr)
    {
        if (Guid.TryParse(dataStr, out var result))
        {
            return result;
        }
        return null;
    }
}
