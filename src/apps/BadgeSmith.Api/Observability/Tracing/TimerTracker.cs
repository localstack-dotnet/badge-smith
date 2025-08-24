using System.Diagnostics;
using Amazon.Lambda.Core;
using BadgeSmith.Api.Observability.Contracts;

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

    public IObservabilityOperation StartOperation(string operationName, ILambdaContext? context = null)
    {
        return new TimerOperation(operationName, context);
    }

    public void Mark(string eventName, ILambdaContext? context = null)
    {
        var ms = MsSince(T0);
        var message = $"mark: {eventName} +{ms:F1} ms";

        if (context != null)
        {
            context.Logger.LogLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }
}
