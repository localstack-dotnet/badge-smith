using System.Diagnostics;
using System.Globalization;

namespace BadgeSmith.Api.Observability;

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

internal static class SimpleLogger
{
    private static string Timestamp => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    public static void LogInformation(string category, string message)
    {
        Console.WriteLine($"{Timestamp}\tinfo: {category}[0]\t{message}");
    }

    public static void LogWarning(string category, string message)
    {
        Console.WriteLine($"{Timestamp}\twarn: {category}[0]\t{message}");
    }

    public static void LogError(string category, string message)
    {
        Console.WriteLine($"{Timestamp}\tfail: {category}[0]\t{message}");
    }

    public static void LogError(string category, Exception ex, string message)
    {
        Console.WriteLine($"{Timestamp}\tfail: {category}[0]\t{message}{Environment.NewLine}{ex}");
    }

    public static void LogDebug(string category, string message)
    {
        Console.WriteLine($"{Timestamp}\tdbug: {category}[0]\t{message}");
    }
}
