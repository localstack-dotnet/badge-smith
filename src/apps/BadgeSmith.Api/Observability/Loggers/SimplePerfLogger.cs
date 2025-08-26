using System.Diagnostics;
using System.Globalization;

namespace BadgeSmith.Api.Observability.Loggers;

internal static class SimplePerfLogger
{
    public static void Log(string message, long t0, string? category = null)
    {
        var timestamp = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        if (ObservabilitySettings.TelemetryFactoryPerfLogs)
        {
            SimpleLogger.LogInformation(category ?? "perf", $"{message} in {timestamp.ToString("F1", CultureInfo.InvariantCulture)} ms");
        }
    }
}
