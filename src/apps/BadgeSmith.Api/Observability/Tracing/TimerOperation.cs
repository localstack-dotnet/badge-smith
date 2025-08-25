#pragma warning disable CA2254

using System.Diagnostics;
using System.Globalization;
using System.Text;
using BadgeSmith.Api.Observability.Contracts;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Observability.Tracing;

/// <summary>
/// Timer-based observability operation that measures duration and supports tagging.
/// </summary>
internal sealed class TimerOperation : IObservabilityOperation
{
    private readonly string _operationName;
    private readonly ILogger? _logger;
    private readonly Stopwatch _stopwatch;
    private readonly Dictionary<string, object?> _tags = new(StringComparer.OrdinalIgnoreCase);
    private string? _status;
    private string? _displayName;
    private Exception? _exception;

    public TimerOperation(string operationName, ILogger? logger = null)
    {
        _operationName = operationName;
        _logger = logger;
        _stopwatch = Stopwatch.StartNew();
    }

    public IObservabilityOperation AddTag(string key, object? value)
    {
        _tags[key] = value;
        return this;
    }

    public IObservabilityOperation SetStatus(string status)
    {
        _status = status;
        return this;
    }

    public IObservabilityOperation SetDisplayName(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    public IObservabilityOperation AddException(Exception exception)
    {
        _exception = exception;
        return this;
    }

    public void Dispose()
    {
        _stopwatch.Stop();

        var message = BuildCompletionMessage();

        if (_logger != null)
        {
            if (_exception != null)
            {
                _logger.LogError(_exception, message);
            }
            else
            {
                _logger.LogInformation(message);
            }
        }
        else
        {
            Console.WriteLine(message);
            if (_exception != null)
            {
                Console.WriteLine($"Exception in {_operationName}: {_exception}");
            }
        }
    }

    private string BuildCompletionMessage()
    {
        var sb = new StringBuilder();
        var displayName = _displayName == null ? string.Empty : $"{_displayName} ";
        sb.AppendLine(CultureInfo.InvariantCulture, $"{displayName}measure: {_operationName} took {_stopwatch.Elapsed.TotalMilliseconds:F1} ms");

        if (!string.IsNullOrEmpty(_status))
        {
            sb.Append($" [status: {_status}]");
        }

        if (_tags.Count == 0)
        {
            return sb.ToString();
        }

        sb.Append(" [");
        var first = true;
        foreach (var tag in _tags)
        {
            if (!first)
            {
                sb.Append(", ");
            }

            sb.Append($"{tag.Key}: {tag.Value}");
            first = false;
        }

        sb.Append(']');

        return sb.ToString();
    }
}
