using System.Diagnostics;
using BadgeSmith.Api.Observability.Contracts;
using BadgeSmith.Api.Observability.Tracing;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1859, RCS1163

namespace BadgeSmith.Api.Observability;

/// <summary>
/// Factory for creating the appropriate observability tracker based on telemetry compilation settings.
/// Returns ActivityTracker when ENABLE_TELEMETRY is defined, TimerTracker otherwise.
/// </summary>
internal static class Tracer
{
    /// <summary>
    /// Creates an observability tracker using the current Activity context.
    /// Uses Activity.Current for telemetry integration when available.
    /// </summary>
    /// <param name="currentActivity">The current activity to enhance, defaults to Activity.Current</param>
    /// <returns>The appropriate tracker based on telemetry compilation settings</returns>
    public static IObservabilityTracker CreateTracker(Activity? currentActivity = null)
    {
#if ENABLE_TELEMETRY
        // Use the provided activity or Activity.Current

        if (!ObservabilitySettings.EnableOtel)
        {
            return new TimerTracker();
        }

        return new ActivityTracker(currentActivity ?? Activity.Current);
#else
        // Use high-performance timer-based tracking (ignores activity)
        return new TimerTracker();
#endif
    }

    /// <summary>
    /// Convenience method for starting an operation with `Activity.Current` integration.
    /// </summary>
    /// <param name="operationName">The name of the operation</param>
    /// <param name="logger">Optional Logger</param>
    /// <param name="currentActivity">Optional specific activity to enhance, defaults to `Activity.Current`</param>
    /// <returns>An observability operation that can be tagged and disposed</returns>
    public static IObservabilityOperation StartOperation(string operationName, ILogger? logger = null, Activity? currentActivity = null)
    {
        var tracker = CreateTracker(currentActivity);
        return tracker.StartOperation(operationName, logger);
    }

    /// <summary>
    /// Convenience method for marking an event with `Activity.Current` integration.
    /// </summary>
    /// <param name="eventName">The name of the event/mark</param>
    /// <param name="logger">Optional Logger</param>
    /// <param name="currentActivity">Optional specific activity to enhance, defaults to Activity.Current</param>
    public static void Mark(string eventName, ILogger? logger = null, Activity? currentActivity = null)
    {
        var tracker = CreateTracker(currentActivity);
        tracker.Mark(eventName, logger);
    }
}
