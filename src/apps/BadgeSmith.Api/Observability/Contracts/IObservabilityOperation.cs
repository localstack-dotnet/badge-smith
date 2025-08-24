namespace BadgeSmith.Api.Observability.Contracts;

/// <summary>
/// Represents an active observability operation that can be annotated and completed.
/// </summary>
internal interface IObservabilityOperation : IDisposable
{
    /// <summary>
    /// Adds a tag/attribute to the current operation.
    /// For timing: logged as structured data
    /// For tracing: added as span attribute
    /// </summary>
    /// <param name="key">The tag key</param>
    /// <param name="value">The tag value</param>
    /// <returns>This operation for fluent chaining</returns>
    IObservabilityOperation AddTag(string key, object? value);

    /// <summary>
    /// Sets the status of the operation.
    /// For timing: logged as part of completion message
    /// For tracing: sets span status
    /// </summary>
    /// <param name="status">The operation status</param>
    /// <returns>This operation for fluent chaining</returns>
    IObservabilityOperation SetStatus(string status);

    /// <summary>
    /// Sets the display name of the operation
    /// </summary>
    /// <param name="displayName">The operation display name</param>
    /// <returns>This operation for fluent chaining</returns>
    IObservabilityOperation SetDisplayName(string displayName);

    /// <summary>
    /// Adds an exception to the operation.
    /// For timing: logged as error information
    /// For tracing: added as span exception
    /// </summary>
    /// <param name="exception">The exception to record</param>
    /// <returns>This operation for fluent chaining</returns>
    IObservabilityOperation AddException(Exception exception);
}
