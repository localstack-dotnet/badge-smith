#if ENABLE_TELEMETRY
using System.Diagnostics;
using BadgeSmith.Api.Observability.Contracts;

namespace BadgeSmith.Api.Observability.Tracing;

/// <summary>
/// Activity-based observability operation that enhances an existing Activity span.
/// </summary>
internal sealed class ActivityOperation : IObservabilityOperation
{
    private readonly Activity? _activity;

    public ActivityOperation(Activity? activity, string operationName)
    {
        _activity = activity;

        // Add operation name as a tag to help identify what we're measuring
        _activity?.SetTag("operation.name", operationName);
    }

    public ActivityOperation(Activity? activity) : this(activity, "unknown")
    {
    }

    public IObservabilityOperation AddTag(string key, object? value)
    {
        _activity?.SetTag(key, value);
        return this;
    }

    public IObservabilityOperation SetStatus(string status)
    {
        _activity?.SetStatus(string.Equals(status, "error", StringComparison.OrdinalIgnoreCase) ? ActivityStatusCode.Error : ActivityStatusCode.Ok, status);
        return this;
    }

    public IObservabilityOperation SetDisplayName(string displayName)
    {
        if (_activity != null)
        {
            _activity.DisplayName = displayName;
        }

        return this;
    }

    public IObservabilityOperation AddException(Exception exception)
    {
        _activity?
            .SetStatus(ActivityStatusCode.Error, exception.Message)
            .AddException(exception);
        return this;
    }

    public void Dispose()
    {
        _activity?.Dispose();
    }
}
#endif
