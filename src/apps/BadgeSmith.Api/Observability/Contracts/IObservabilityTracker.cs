using Amazon.Lambda.Core;

namespace BadgeSmith.Api.Observability.Contracts;

/// <summary>
/// Unified observability interface that abstracts over both performance timing (BootTimer)
/// and distributed tracing (ActivitySource) based on telemetry compilation settings.
/// </summary>
internal interface IObservabilityTracker
{
    /// <summary>
    /// Starts a new observability operation with the specified name.
    /// </summary>
    /// <param name="operationName">The name of the operation being tracked</param>
    /// <param name="context">Optional Lambda context for logging</param>
    /// <returns>An operation that can be tagged and disposed to complete tracking</returns>
    IObservabilityOperation StartOperation(string operationName, ILambdaContext? context = null);

    /// <summary>
    /// Marks a point-in-time event for observability.
    /// </summary>
    /// <param name="eventName">The name of the event/mark</param>
    /// <param name="context">Optional Lambda context for logging</param>
    void Mark(string eventName, ILambdaContext? context = null);
}
