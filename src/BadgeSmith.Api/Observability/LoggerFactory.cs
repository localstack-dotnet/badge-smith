using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

#if ENABLE_TELEMETRY
using static System.Environment;
using OpenTelemetry.Logs;
#endif

namespace BadgeSmith.Api.Observability;

/// <summary>
/// Static logger factory providing ILogger instances with OpenTelemetry integration and environment-based configuration.
/// Supports both console logging for Lambda visibility and structured logging with automatic trace correlation.
/// </summary>
internal static class LoggerFactory
{
    private static readonly Lazy<ILoggerFactory> Factory = new(CreateFactory());

    /// <summary>
    /// Creates a typed logger instance for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to create a logger for</typeparam>
    /// <returns>A logger instance with OpenTelemetry integration when telemetry is enabled</returns>
    public static ILogger<T> CreateLogger<T>()
    {
        return Factory.Value.CreateLogger<T>();
    }

    /// <summary>
    /// Creates a logger instance for the specified category name.
    /// </summary>
    /// <param name="categoryName">The category name for the logger</param>
    /// <returns>A logger instance with OpenTelemetry integration when telemetry is enabled</returns>
    public static ILogger CreateLogger(string categoryName)
    {
        return Factory.Value.CreateLogger(categoryName);
    }

    /// <summary>
    /// Creates the internal logger factory with appropriate providers based on compilation settings.
    /// </summary>
    private static ILoggerFactory CreateFactory()
    {
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            if (string.Equals(ObservabilitySettings.DotNetEnvironment, "Production", StringComparison.Ordinal))
            {
                builder
                    .SetMinimumLevel(LogLevel.Information)
                    .AddFilter("System", LogLevel.Error)
                    .AddFilter("Microsoft", LogLevel.Error)
                    .AddFilter("AWSSDK", LogLevel.Error);
            }
            else
            {
                builder
                    .SetMinimumLevel(LogLevel.Information)
                    .AddFilter("System", LogLevel.Information)
                    .AddFilter("Microsoft", LogLevel.Information)
                    .AddFilter("AWSSDK", LogLevel.Information);
            }

            builder.Configure(options => options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId | ActivityTrackingOptions.TraceId);

            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ\t";
                options.UseUtcTimestamp = true;
                options.IncludeScopes = true;
                options.ColorBehavior = LoggerColorBehavior.Disabled;
            });

#if ENABLE_TELEMETRY
            builder.AddOpenTelemetry(options =>
            {
                options.AddOtlpExporter(exporterOptions =>
                {
                    var endpoint = GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
                    if (!string.IsNullOrWhiteSpace(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
                    {
                        exporterOptions.Endpoint = uri;
                    }
                });

                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
            });
#endif
        });

        return loggerFactory;
    }
}
