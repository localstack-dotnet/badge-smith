using System.Diagnostics;
using BadgeSmith.Api.Observability.Contracts;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Observability.Tracing;

/// <summary>
/// BootTimer-based observability tracker for high-performance timing without distributed tracing overhead.
/// Used when ENABLE_TELEMETRY is false to provide lightweight performance monitoring.
/// </summary>
internal sealed class TimerTracker : IObservabilityTracker
{
    private static readonly long T0 = Stopwatch.GetTimestamp();

    private static double MsSince(long ticks) =>
        (Stopwatch.GetTimestamp() - ticks) * 1000.0 / Stopwatch.Frequency;

    public IObservabilityOperation StartOperation(string operationName, ILogger? logger = null)
    {
        return new TimerOperation(operationName, logger);
    }

    public void Mark(string eventName, ILogger? logger = null)
    {
        var ms = MsSince(T0);

        if (logger != null)
        {
            logger.LogInformation("mark: {EventName} +{Ms:F1} ms", eventName, ms);
        }
        else
        {
            Console.WriteLine($"mark: {eventName} +{ms:F1} ms");
        }
    }
}
