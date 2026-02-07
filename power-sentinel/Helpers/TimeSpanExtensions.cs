using System;

namespace PowerSentinel.Helpers;

public static class TimeSpanExtensions
{
    public static string ToDisplayString(this TimeSpan? ts)
    {
        if (ts == null)
            return string.Empty;

        if (ts.Value.TotalDays > 1)
            return $"{(int)ts.Value.TotalDays:D}d {ts.Value.Hours}hr {ts.Value.Minutes:D}min";

        if (ts.Value.TotalHours > 1)
            return $"{(int)ts.Value.TotalHours:D}hr {ts.Value.Minutes:D2}min";

        return $"{ts.Value.Minutes:D}min";
    }
}
