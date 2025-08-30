using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using BadgeSmith.Api.Observability.Loggers;

namespace BadgeSmith.Api.Observability.Performance;

/// <summary>
/// Native AOT-friendly performance tracker for scoped timing measurements.
/// Thread-safe and designed for minimal overhead in serverless environments.
/// </summary>
internal static class PerfTracker
{
    /// <summary>
    /// Use ThreadStatic to avoid contention in Lambda (single-threaded per invocation)
    /// </summary>
    [ThreadStatic] private static Stack<PerfScope>? _scopeStack;

    /// <summary>
    /// Creates a performance scope that will automatically log when disposed.
    /// Perfect for using with 'using' statements.
    /// </summary>
    /// <param name="operationName">Name of the operation being measured</param>
    /// <param name="category">Optional logging category</param>
    /// <returns>A disposable scope that logs duration on disposal</returns>
    public static PerfScope StartScope(string operationName, string? category = null)
    {
        _scopeStack ??= new Stack<PerfScope>();

        var scope = new PerfScope(operationName, category, _scopeStack.Count);
        _scopeStack.Push(scope);

        return scope;
    }

    /// <summary>
    /// Measures the execution time of a synchronous operation.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The operation to measure</param>
    /// <param name="operationName">Name for logging</param>
    /// <param name="category">Optional logging category</param>
    /// <returns>The result of the operation</returns>
    public static T Measure<T>(Func<T> operation, string operationName, string? category = null)
    {
        using var scope = StartScope(operationName, category);
        return operation();
    }

    /// <summary>
    /// Measures the execution time of an asynchronous operation.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The async operation to measure</param>
    /// <param name="operationName">Name for logging</param>
    /// <param name="category">Optional logging category</param>
    /// <returns>The result of the operation</returns>
    public static async Task<T> MeasureAsync<T>(Func<Task<T>> operation, string operationName, string? category = null)
    {
        using var scope = StartScope(operationName, category);
        return await operation().ConfigureAwait(false);
    }

    /// <summary>
    /// Simple timing method that measures from a specific timestamp to now.
    /// </summary>
    /// <param name="startTimestamp">The starting timestamp from Stopwatch.GetTimestamp()</param>
    /// <param name="message">Description of what was measured</param>
    /// <param name="category">Optional logging category</param>
    public static void LogElapsed(long startTimestamp, string message, string? category = null)
    {
        var elapsed = GetElapsedMilliseconds(startTimestamp);
        LogPerformance(message, elapsed, category);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }

    private static void LogPerformance(string message, double milliseconds, string? category)
    {
        if (!ObservabilitySettings.TelemetryFactoryPerfLogs)
        {
            return;
        }

        var formattedTime = milliseconds.ToString("F1", CultureInfo.InvariantCulture);
        SimpleLogger.LogInformation(category ?? "perf", $"Δ {message} in {formattedTime} ms");
    }

    /// <summary>
    /// A disposable scope for automatic performance measurement.
    /// Use with 'using' statements for clean, exception-safe timing.
    /// </summary>
    internal readonly struct PerfScope : IDisposable
    {
        private readonly long _startTimestamp;
        private readonly string _operationName;
        private readonly string? _category;
        private readonly int _depth;

        internal PerfScope(string operationName, string? category, int depth)
        {
            _startTimestamp = Stopwatch.GetTimestamp();
            _operationName = operationName;
            _category = category;
            _depth = depth;
        }

        /// <summary>
        /// Logs elapsed time from the scope's start time to now with a custom message.
        /// Useful for logging milestones within a longer operation.
        /// </summary>
        /// <param name="message">Description of the milestone reached</param>
        public void LogMilestone(string message)
        {
            var elapsed = (Stopwatch.GetTimestamp() - _startTimestamp) * 1000.0 / Stopwatch.Frequency;
            var indent = new string(' ', (_depth + 1) * 2); // Indent more than parent scope
            var formattedTime = elapsed.ToString("F1", CultureInfo.InvariantCulture);

            if (ObservabilitySettings.TelemetryFactoryPerfLogs)
            {
                SimpleLogger.LogInformation(_category ?? "perf", $"Δ {indent}→ {message} in {formattedTime} ms");
            }
        }

        public void Dispose()
        {
            CompleteScopeInternal(this);
        }

        private static void CompleteScopeInternal(PerfScope scope)
        {
            _scopeStack?.TryPop(out _);

            var elapsed = GetElapsedMilliseconds(scope._startTimestamp);
            var indent = new string(' ', scope._depth * 2);

            LogPerformance(indent + scope._operationName, elapsed, scope._category);
        }
    }
}
