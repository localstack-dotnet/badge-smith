#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using System.Globalization;
using BadgeSmith;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using Serilog.Sinks.OpenTelemetry;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
/// This project should be referenced by each service project in your solution.
/// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
/// </summary>
public static class BadgeSmithObservabilityExtensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureSerilog();
        builder.ConfigureOpenTelemetry();
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        return builder;
    }

    private static TBuilder ConfigureSerilog<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.ClearProviders();

        builder.Services.AddSerilog(configuration =>
        {
            var consoleLogging = builder.Configuration.GetSection("LoggingDestinations:ConsoleLogging").Value;
            var enableJsonLogging = builder.Configuration.GetSection("LoggingDestinations:EnableJsonLogging").Value;
            var openTelemetry = builder.Configuration.GetSection("LoggingDestinations:OpenTelemetry").Value;

            if (builder.Environment.IsProduction())
            {
                configuration.MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Error)
                    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Error);
            }
            else if (builder.Environment.IsDevelopment())
            {
                configuration.MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
                    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Information)
                    .MinimumLevel.Override("Serilog", Serilog.Events.LogEventLevel.Information)
                    .MinimumLevel.Override("AWSSDK", Serilog.Events.LogEventLevel.Information);
            }
            else
            {
                configuration.MinimumLevel.Information();
            }

            configuration
                .Enrich.FromLogContext()
                .Enrich.WithProperty("OtelResourceAttributes", builder.Configuration["OTEL_RESOURCE_ATTRIBUTES"])
                .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName);

            if (bool.TryParse(consoleLogging, out var useConsoleLogging) && useConsoleLogging)
            {
                if (bool.TryParse(enableJsonLogging, out var useEnableJsonLogging) && useEnableJsonLogging)
                {
                    configuration.WriteTo.Console(new JsonFormatter(renderMessage: true));
                }
                else
                {
                    configuration.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
                }
            }

            if (bool.TryParse(openTelemetry, out var useOpenTelemetry) && useOpenTelemetry)
            {
                configuration.WriteTo.OpenTelemetry(cfg =>
                {
                    cfg.Endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
                    cfg.IncludedData = IncludedData.TraceIdField | IncludedData.SpanIdField;
                    cfg.ResourceAttributes =
                        new Dictionary<string, object>
                            (StringComparer.Ordinal)
                            {
                                { "service.name", builder.Environment.ApplicationName },
                                { "deployment.environment", builder.Environment.EnvironmentName },
                            };
                });
            }
        });

        return builder;
    }

    private static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // builder.Logging.AddOpenTelemetry(logging =>
        // {
        //     logging.IncludeFormattedMessage = true;
        //     logging.IncludeScopes = true;
        // });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                // .AddAspNetCoreInstrumentation()
                metrics
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                // .AddAspNetCoreInstrumentation()
                tracing
                    .AddHttpClientInstrumentation(options =>
                    {
                        var runtimeAuthority = Environment.GetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API");
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
                            var uri = request.RequestUri;
                            if (uri is null)
                            {
                                return true;
                            }

                            // 1) Compare to env-provided host:port (preferred)
                            if (!string.IsNullOrWhiteSpace(runtimeHost) &&
                                uri.Host.Equals(runtimeHost, StringComparison.OrdinalIgnoreCase) &&
                                (!runtimePort.HasValue || uri.Port == runtimePort.Value))
                            {
                                return false;
                            }

                            // 2) Fallback: any Lambda Runtime API path (covers if host compare failed)
                            return !uri.AbsolutePath.StartsWith("/2018-06-01/runtime/", StringComparison.Ordinal);
                        };
                    })
                    .AddAWSInstrumentation()
                    .AddAWSLambdaConfigurations(options => options.DisableAwsXRayContextExtraction = true)
                    .AddSource(BadgeSmithInfrastructureActivitySource.ActivitySourceName)
                    .AddSource(BadgeSmithApiActivitySource.ActivitySourceName);
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    // public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    // {
    //     builder.Services.AddHealthChecks()
    //         .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
    //
    //     return builder;
    // }

    // public static WebApplication MapDefaultEndpoints(this WebApplication app)
    // {
    //     ArgumentNullException.ThrowIfNull(app);
    //
    //     // Adding health checks endpoints to applications in non-development environments has security implications.
    //     // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
    //     if (!app.Environment.IsDevelopment())
    //     {
    //         return app;
    //     }
    //
    //     // All health checks must pass for app to be considered ready to accept traffic after starting
    //     app.MapHealthChecks("/health");
    //
    //     // Only health checks tagged with the "live" tag must pass for app to be considered alive
    //     app.MapHealthChecks("/alive", new HealthCheckOptions
    //     {
    //         Predicate = r => r.Tags.Contains("live"),
    //     });
    //
    //     return app;
    // }
}
