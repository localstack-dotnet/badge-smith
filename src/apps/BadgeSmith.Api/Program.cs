#pragma warning disable CA1502

using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using BadgeSmith.Api.Handlers;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Observability;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;
using LoggerFactory = BadgeSmith.Api.Observability.LoggerFactory;

#if ENABLE_TELEMETRY
using BadgeSmith;
using static BadgeSmith.Api.Observability.ObservabilitySettings;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.AWSLambda;
#endif

#if ENABLE_TELEMETRY
using var tracerProvider = TelemetryFactory.CreateTracerProvider(ApplicationName, ApplicationVersion);
using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity("Lambda Initialization");
#endif

var appStart = Stopwatch.GetTimestamp();
var apiRouter = BuildRouter();

#if ENABLE_TELEMETRY
var handler = BuildHandler(tracerProvider, apiRouter);
#else
var handler = BuildHandler(apiRouter);
#endif

var jsonSerializer = new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>();
var lambdaBootstrap = LambdaBootstrapBuilder.Create(handler, jsonSerializer).Build();

SimplePerfLogger.Log("Lambda Initialization Complete", appStart, "Program.cs");
#if ENABLE_TELEMETRY
activity?.Dispose();
#endif
await lambdaBootstrap.RunAsync().ConfigureAwait(false);

return;

static ApiRouter BuildRouter()
{
    var logger = LoggerFactory.CreateLogger<ApiRouter>();

    var routeResolver = new RouteResolver(RouteTable.Routes);
    var handlerFactory = new HandlerFactory();

    return new ApiRouter(logger, routeResolver, handlerFactory);
}

#if ENABLE_TELEMETRY
static Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>> BuildHandler(TracerProvider tracerProvider, ApiRouter apiRouter)
{
    if (EnableOtel)
    {
        return (req, ctx) =>
            AWSLambdaWrapper.TraceAsync(
                tracerProvider,
                (wrappedReq, wrappedCtx) => FunctionCoreAsync(wrappedReq, wrappedCtx, apiRouter),
                req, ctx);
    }

    return (req, ctx) => FunctionCoreAsync(req, ctx, apiRouter);
}
#else
static Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>> BuildHandler(ApiRouter apiRouter)
{
    return (req, ctx) => FunctionCoreAsync(req, ctx, apiRouter);
}
#endif

static async Task<APIGatewayHttpApiV2ProxyResponse> FunctionCoreAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context, ApiRouter apiRouter)
{
#if ENABLE_TELEMETRY
    SetHttpTags(request, context);
#endif

    var httpMethod = request.RequestContext.Http.Method ?? "UNKNOWN";
    var path = request.RequestContext.Http.Path ?? "/";

    var logger = LoggerFactory.CreateLogger<Program>();
    using var beginScope = logger.BeginScope(context.AwsRequestId);
    logger.LogInformation("Handling {Method} {Path}", httpMethod, path);

    try
    {
        return await apiRouter.RouteAsync(path, httpMethod, request.Headers).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unhandled error");
        return ResponseHelper.InternalServerError("An error occurred processing the request");
    }
}

#if ENABLE_TELEMETRY
static void SetHttpTags(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
{
    var activity = Activity.Current;

    var httpMethod = request.RequestContext.Http.Method ?? "UNKNOWN";
    var path = request.RequestContext.Http.Path ?? "/";
    var routeKey = request.RequestContext.RouteKey; // e.g., "GET /badges/{id}"
    var route = routeKey?.IndexOf(' ', StringComparison.OrdinalIgnoreCase) is { } idx and >= 0 ? routeKey[(idx + 1)..] : null;

    // Add comprehensive HTTP context to our unified operation
    activity?
        .AddTag("http.method", httpMethod)
        .AddTag("http.path", path)
        .AddTag("http.route", route ?? path)
        .AddTag("request.id", context.AwsRequestId);

    if (activity != null)
    {
        activity.DisplayName = $"{httpMethod} {route ?? path}";
    }

    // Add headers and stage information
    if (request.Headers?.TryGetValue("host", out var hostHeader) == true)
    {
        activity?.AddTag("server.address", hostHeader);
    }

    if (request.Headers?.TryGetValue("x-forwarded-proto", out var proto) == true)
    {
        activity?.AddTag("url.scheme", proto);
    }

    if (request.RequestContext?.Stage is { } stage)
    {
        activity?.AddTag("server.port", string.Equals(stage, "$default", StringComparison.OrdinalIgnoreCase) ? null : stage);
    }
}
#endif
