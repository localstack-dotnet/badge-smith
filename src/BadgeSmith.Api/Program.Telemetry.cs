#if ENABLE_TELEMETRY
#pragma warning disable CA1502

using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Observability;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;
using LoggerFactory = BadgeSmith.Api.Observability.LoggerFactory;
using BadgeSmith;
using static BadgeSmith.Api.Observability.ObservabilitySettings;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.AWSLambda;

using var tracerProvider = TelemetryFactory.CreateTracerProvider(ApplicationName, ApplicationVersion);
LambdaBootstrap lambdaBootstrap;
using (var _ = BadgeSmithApiActivitySource.ActivitySource.StartActivity("Lambda Initialization"))
{
    var apiRouter = ApiRouterBuilder.BuildApiRouter();
    var handler = BuildHandler(tracerProvider, apiRouter);

    var jsonSerializer = new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>();
    lambdaBootstrap = LambdaBootstrapBuilder.Create(handler, jsonSerializer).Build();
}

await lambdaBootstrap.RunAsync().ConfigureAwait(false);

return;

static Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>> BuildHandler(TracerProvider tracerProvider, ApiRouter apiRouter)
{
    return (req, ctx) =>
        AWSLambdaWrapper.TraceAsync(
            tracerProvider,
            (wrappedReq, wrappedCtx) => FunctionCoreAsync(wrappedReq, wrappedCtx, apiRouter),
            req, ctx);
}

static async Task<APIGatewayHttpApiV2ProxyResponse> FunctionCoreAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context, ApiRouter apiRouter)
{
    SetHttpTags(request, context);

    // var timeout = context.RemainingTime.Subtract(TimeSpan.FromSeconds(5));
    // using var cts = new CancellationTokenSource(timeout);

    var httpMethod = request.RequestContext.Http.Method ?? "UNKNOWN";
    var path = request.RequestContext.Http.Path ?? "/";

    var logger = LoggerFactory.CreateLogger<Program>();
    using var beginScope = logger.BeginScope(context.AwsRequestId);
    logger.LogInformation("Handling {Method} {Path}", httpMethod, path);

    try
    {
        return await apiRouter.RouteAsync(request).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unhandled error");
        return ResponseHelper.InternalServerError("An error occurred processing the request");
    }
}

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
