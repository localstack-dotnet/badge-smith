#if ENABLE_TELEMETRY
using System.Diagnostics;
using Amazon.Lambda.Core;
using BadgeSmith.Api.Observability.Contracts;

namespace BadgeSmith.Api.Observability.Tracing;

/// <summary>
/// Activity.Current-based observability tracker for distributed tracing when telemetry is enabled.
/// Works with existing Activity spans created by Lambda runtime/OpenTelemetry instead of creating new ones.
/// </summary>
internal sealed class ActivityTracker : IObservabilityTracker
{
    private readonly Activity? _currentActivity;

    public ActivityTracker(Activity? currentActivity)
    {
        _currentActivity = currentActivity;
    }

    public IObservabilityOperation StartOperation(string operationName, ILambdaContext? context = null)
    {
        // Don't create new activities - enhance the current one
        // For nested operations, we could create child spans, but for now keep it simple
        return new ActivityOperation(_currentActivity, operationName);
    }

    public void Mark(string eventName, ILambdaContext? context = null)
    {
        // Add event to current activity if available
        _currentActivity?.AddEvent(new ActivityEvent(eventName));

        // Also log for immediate visibility in Lambda logs
        if (context != null)
        {
            context.Logger.LogLine($"event: {eventName}");
        }
        else
        {
            Console.WriteLine($"event: {eventName}");
        }
    }
}

#endif
