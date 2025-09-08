#if ENABLE_TELEMETRY
using System.Globalization;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using static System.Environment;

namespace BadgeSmith.Api.Core.Observability;

/// <summary>
/// Factory for creating OpenTelemetry TracerProvider without DI container dependency.
/// Provides the same instrumentation as BadgeSmithObservabilityExtensions but with direct SDK usage.
/// </summary>
internal static class TelemetryFactory
{
    public static TracerProvider CreateTracerProvider(string serviceName, string? serviceVersion = null)
    {
        return Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateEmpty()
                .AddService(serviceName, serviceVersion)
                .AddAttributes([
                    new KeyValuePair<string, object>("deployment.environment", Settings.DotNetEnvironment),
                ]))
            .AddSource("BadgeSmith.*")
            .AddHttpClientInstrumentation(ConfigureHttpInstrumentation)
            .AddAWSInstrumentation()
            .AddAWSLambdaConfigurations(options => options.DisableAwsXRayContextExtraction = true)
            .AddOtlpExporter(ConfigureOtlpExporter)
            .Build();
    }

    private static void ConfigureHttpInstrumentation(HttpClientTraceInstrumentationOptions options)
    {
        var runtimeAuthority = GetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API");
        string? runtimeHost = null;
        int? runtimePort = null;

        if (!string.IsNullOrWhiteSpace(runtimeAuthority))
        {
            var parts = runtimeAuthority.Split(':', 2, StringSplitOptions.TrimEntries);
            runtimeHost = parts.Length > 0 ? parts[0] : null;

            if (parts.Length == 2 && int.TryParse(parts[1], CultureInfo.InvariantCulture, out var p))
            {
                runtimePort = p;
            }
        }

        options.FilterHttpRequestMessage = request =>
        {
            var requestRequestUri = request.RequestUri;
            if (requestRequestUri is null)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(runtimeHost) &&
                requestRequestUri.Host.Equals(runtimeHost, StringComparison.OrdinalIgnoreCase) &&
                (!runtimePort.HasValue || requestRequestUri.Port == runtimePort.Value))
            {
                return false;
            }

            return !requestRequestUri.AbsolutePath.StartsWith("/2018-06-01/runtime/", StringComparison.OrdinalIgnoreCase);
        };
    }

    private static void ConfigureOtlpExporter(OtlpExporterOptions options)
    {
        var endpoint = GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            options.Endpoint = uri;
        }
    }
}
#endif
