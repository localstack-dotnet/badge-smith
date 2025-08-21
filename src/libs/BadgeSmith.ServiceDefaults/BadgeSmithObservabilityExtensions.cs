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

    public static TBuilder ConfigureSerilog<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.ClearProviders();

        builder.Services.AddSerilog(configuration =>
        {
            var consoleLogging = builder.Configuration.GetSection("LoggingDestinations:ConsoleLogging").Value;
            var enableJsonLogging = builder.Configuration.GetSection("LoggingDestinations:EnableJsonLogging").Value;
            var openTelemetry = builder.Configuration.GetSection("LoggingDestinations:OpenTelemetry").Value;

            configuration.ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironment("OTEL_RESOURCE_ATTRIBUTES")
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

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
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
                    .AddHttpClientInstrumentation()
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
