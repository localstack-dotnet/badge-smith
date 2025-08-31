using System.Diagnostics;
using System.Globalization;
using BadgeSmith.Api.Observability.Loggers;

#pragma warning disable RCS1093

namespace BadgeSmith.Api.Observability.Performance;

internal static class PerfTracker
{
    public static PerfScope StartScope(string operationName, string? category = null) => new(operationName, category);

    internal readonly struct PerfScope : IDisposable
    {
        private readonly long _startTimestamp;
        private readonly string _operationName;
        private readonly string? _category;

        internal PerfScope(string operationName, string? category)
        {
            _startTimestamp = Stopwatch.GetTimestamp();
            _operationName = operationName;
            _category = category;
        }

        public void Dispose()
        {
            if (!ObservabilitySettings.TelemetryFactoryPerfLogs)
            {
                return;
            }

            var elapsed = (Stopwatch.GetTimestamp() - _startTimestamp) * 1000.0 / Stopwatch.Frequency;
            var formattedTime = elapsed.ToString("F1", CultureInfo.InvariantCulture);
            SimpleLogger.LogInformation(_category ?? "perf", $"Δ {_operationName} in {formattedTime} ms");
        }
    }
}
