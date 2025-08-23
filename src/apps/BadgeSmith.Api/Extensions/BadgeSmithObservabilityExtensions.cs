#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using static System.Environment;

namespace Microsoft.Extensions.Hosting;

internal static class BadgeSmithObservabilityExtensions
{
    public static void ConfigureOpenTelemetry(this IServiceCollection services, IAppEnvironment env, string serviceName, string? serviceVersion = null)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(rb =>
            {
                rb.AddService(serviceName: serviceName, serviceVersion: serviceVersion);
                rb.AddAttributes([new KeyValuePair<string, object>("deployment.environment", env.EnvironmentName)]);
            })
            .WithTracing(tracerBuilder =>
            {
                tracerBuilder.AddSource("BadgeSmith.*");
                tracerBuilder.AddHttpClientInstrumentation(options =>
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

                        // 1) Compare to env-provided host:port (preferred)
                        if (!string.IsNullOrWhiteSpace(runtimeHost) &&
                            requestRequestUri.Host.Equals(runtimeHost, StringComparison.OrdinalIgnoreCase) &&
                            (!runtimePort.HasValue || requestRequestUri.Port == runtimePort.Value))
                        {
                            return false;
                        }

                        // 2) Fallback: any Lambda Runtime API path (covers if host compare failed)
                        return !requestRequestUri.AbsolutePath.StartsWith("/2018-06-01/runtime/", StringComparison.Ordinal);
                    };
                });

                tracerBuilder.AddAWSInstrumentation();
                tracerBuilder.AddAWSLambdaConfigurations(options => options.DisableAwsXRayContextExtraction = true);

                var endpoint = GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
                if (!string.IsNullOrWhiteSpace(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
                {
                    tracerBuilder.AddOtlpExporter(o => o.Endpoint = uri);
                }
            });
    }
}

internal interface IAppEnvironment
{
    public string EnvironmentName { get; }
    public bool IsDevelopment { get; }
    public bool IsProduction { get; }
}

internal sealed class EnvFromVariables(string? name = null) : IAppEnvironment
{
    public string EnvironmentName { get; } = string.IsNullOrWhiteSpace(name)
        ? GetEnvironmentVariable("DOTNET_ENVIRONMENT")
          ?? GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
          ?? "Production"
        : name;

    public bool IsDevelopment => string.Equals(EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);

    public bool IsProduction => string.Equals(EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase);
}
