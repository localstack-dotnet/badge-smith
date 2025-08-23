#pragma warning disable CA1502

using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using BadgeSmith.Api;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

BootTimer.Mark(ctx: null, "program-start");

// Build router during init instead of on first request for better cold start
var apiRouter = BuildRouter();
BootTimer.Mark(ctx: null, "api-router-built");

var _ = bool.TryParse(Environment.GetEnvironmentVariable("ObservabilityOptions:EnableOtel"), out var enableOtel);
BootTimer.Mark(ctx: null, "enable-otel-init");

var handler = BuildHandler(apiRouter, enableOtel);
BootTimer.Mark(ctx: null, "handler-built");

var jsonSerializer = new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>();
BootTimer.Mark(ctx: null, "json-serializer-created");

var lambdaBootstrap = LambdaBootstrapBuilder.Create(handler, jsonSerializer)
    .Build();
BootTimer.Mark(ctx: null, "lambda-bootstrap-init");

await lambdaBootstrap.RunAsync().ConfigureAwait(false);

return;

static ApiRouter BuildRouter()
{
    BootTimer.Mark(null, "router-build-start");
    // Use single route array - RouteResolverV2 splits internally while preserving order
    var routeResolver = new RouteResolverV2(RouteTableV2.Routes);
    // Use pre-initialized handler registry instead of factory pattern
    var r = new ApiRouter(routeResolver);
    BootTimer.Mark(null, "router-build-end");
    return r;
}

static Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>> BuildHandler(ApiRouter apiRouter, bool enableOtel)
{
    BootTimer.Mark(null, "handler-builder-entry");
    var lazyProvider = new Lazy<TracerProvider?>(CreateTracerProvider);

    if (enableOtel && lazyProvider.Value is not null)
    {
        return (req, ctx) =>
            AWSLambdaWrapper.TraceAsync(
                lazyProvider.Value,
                (wrappedReq, wrappedCtx) => FunctionCoreAsync(wrappedReq, wrappedCtx, apiRouter),
                req, ctx);
    }

    return (req, ctx) => FunctionCoreAsync(req, ctx, apiRouter);
}

static async Task<APIGatewayHttpApiV2ProxyResponse> FunctionCoreAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context, ApiRouter apiRouter)
{
    BootTimer.Mark(context, "first-handler-entry");
    var httpMethod = request.RequestContext.Http.Method ?? "UNKNOWN";
    var path = request.RequestContext.Http.Path ?? "/";
    var routeKey = request.RequestContext.RouteKey; // e.g., "GET /badges/{id}"
    var route = routeKey?.IndexOf(' ', StringComparison.OrdinalIgnoreCase) is { } idx and >= 0 ? routeKey[(idx + 1)..] : null;

    var span = Activity.Current;

    var name = $"{httpMethod} {route ?? path}";
    if (span is not null)
    {
        span.DisplayName = name;
        span.SetTag("http.request.method", httpMethod)
            .SetTag("url.path", path);

        if (!string.IsNullOrEmpty(route))
        {
            span.SetTag("http.route", route);
        }

        if (request.Headers?.TryGetValue("host", out var hostHeader) == true)
        {
            span.SetTag("server.address", hostHeader);
        }

        if (request.Headers?.TryGetValue("x-forwarded-proto", out var proto) == true)
        {
            span.SetTag("url.scheme", proto);
        }

        if (request.RequestContext?.Stage is { } stage)
        {
            span.SetTag("server.port", string.Equals(stage, "$default", StringComparison.OrdinalIgnoreCase) ? null : stage);
        }
    }

    context.Logger.LogInformation("Handling {Method} {Path}", httpMethod, path);

    APIGatewayHttpApiV2ProxyResponse? response = null;

    try
    {
        using (BootTimer.Measure(context, "route-dispatch"))
        {
            return await apiRouter.RouteAsync(request, context).ConfigureAwait(false);
        }
    }
    catch (Exception ex)
    {
        span?.SetStatus(ActivityStatusCode.Error, ex.Message);
        span?.AddException(ex);
        context.Logger.LogError(ex, "Unhandled error");
        return ResponseHelper.InternalServerError("An error occurred processing the request");
    }
    finally
    {
        if (Activity.Current is { } s && response is not null)
        {
            s.SetTag("http.response.status_code", response.StatusCode);
        }
    }
}

static TracerProvider CreateTracerProvider()
{
    BootTimer.Mark(ctx: null, "create-tracer-provider-init-start");
    var env = new EnvFromVariables();
    var services = new ServiceCollection();

    services.ConfigureOpenTelemetry(env, serviceName: "badge-smith-api", serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString());

    var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
    {
        ValidateScopes = false,
    });
    BootTimer.Mark(ctx: null, "create-tracer-provider-init-end");
    var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();
    BootTimer.Mark(ctx: null, "create-tracer-provider-created");
    return tracerProvider;
}
