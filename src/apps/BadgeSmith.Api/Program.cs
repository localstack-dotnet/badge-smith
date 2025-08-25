#pragma warning disable CA1502

using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Observability.Contracts;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;
using LoggerFactory = BadgeSmith.Api.Observability.LoggerFactory;
using Tracer = BadgeSmith.Api.Observability.Tracer;

#if ENABLE_TELEMETRY
using BadgeSmith;
using BadgeSmith.Api.Observability;
using static BadgeSmith.Api.Observability.ObservabilitySettings;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.AWSLambda;
#endif

LambdaBootstrap lambdaBootstrap;
#if ENABLE_TELEMETRY
using var tracerProvider = TelemetryFactory.CreateProvider(ApplicationName, ApplicationVersion);
using var initActivity = BadgeSmithApiActivitySource.ActivitySource.StartActivity();
using (var _ = Tracer.StartOperation("lambda-initialization", currentActivity: initActivity))
#else
using (var _ = Tracer.StartOperation("lambda-initialization"))
#endif
{
    var stopwatch = new Stopwatch();
    stopwatch.Start();
    var logger = LoggerFactory.CreateLogger<Program>();
    Console.WriteLine($"Logger created: {stopwatch.ElapsedMilliseconds} ms");
    Tracer.Mark("program-start", logger: logger);

    var apiRouter = BuildRouter();
    Tracer.Mark("api-router-built", logger: logger);

#if ENABLE_TELEMETRY
    var handler = BuildHandler(tracerProvider, logger, apiRouter);
#else
    var handler = BuildHandler(logger, apiRouter);
#endif
    Tracer.Mark("handler-built", logger: logger);

    var jsonSerializer = new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>();
    Tracer.Mark("json-serializer-created", logger: logger);

    lambdaBootstrap = LambdaBootstrapBuilder.Create(handler, jsonSerializer).Build();
    Tracer.Mark("lambda-bootstrap-init", logger: logger);
}

await lambdaBootstrap.RunAsync().ConfigureAwait(false);

return;

static ApiRouter BuildRouter()
{
    using var operation = Tracer.StartOperation("api-router-build");
    Tracer.Mark("api-router-build-start");

    var routeResolver = new RouteResolver(RouteTable.Routes);
    var router = new ApiRouter(routeResolver);

    operation.AddTag("route-count", RouteTable.Routes.Length);
    return router;
}

#if ENABLE_TELEMETRY
static Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>> BuildHandler(TracerProvider tracerProvider, ILogger logger,
    ApiRouter apiRouter)
{
    using var operation = Tracer.StartOperation("build-handler", logger: logger);
    Tracer.Mark("build-handler-entry", logger: logger);

    if (EnableOtel)
    {
        return (req, ctx) =>
            AWSLambdaWrapper.TraceAsync(
                tracerProvider,
                (wrappedReq, wrappedCtx) => FunctionCoreAsync(wrappedReq, wrappedCtx, logger, apiRouter),
                req, ctx);
    }

    return (req, ctx) => FunctionCoreAsync(req, ctx, logger, apiRouter);
}
#else
static Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>> BuildHandler(ILogger logger, ApiRouter apiRouter)
{
    using var operation = Tracer.StartOperation("build-handler");
    Tracer.Mark("build-handler-entry");
    return (req, ctx) => FunctionCoreAsync(req, ctx, logger, apiRouter);
}
#endif

static async Task<APIGatewayHttpApiV2ProxyResponse> FunctionCoreAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context, ILogger logger, ApiRouter apiRouter)
{
    using var operation = Tracer.StartOperation("main-handler-total", logger: logger);
    Tracer.Mark("main-handler-entry", logger: logger);

    var (httpMethod, path) = SetHttpTags(operation, request, context);

    context.Logger.LogInformation("Handling {Method} {Path}", httpMethod, path);

    try
    {
        using var routeOperation = Tracer.StartOperation("route-dispatch", logger: logger);
        var apiResponse = await apiRouter.RouteAsync(request, context).ConfigureAwait(false);

        routeOperation.AddTag("response.status", apiResponse.StatusCode);
        operation.AddTag("response.status", apiResponse.StatusCode);

        return apiResponse;
    }
    catch (Exception ex)
    {
        // Use our unified observability for error handling
        operation.AddException(ex).SetStatus("error");

        logger.LogError(ex, "Unhandled error");
        return ResponseHelper.InternalServerError("An error occurred processing the request");
    }
}

static (string httpMethod, string path) SetHttpTags(IObservabilityOperation operation, APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
{
    var httpMethod = request.RequestContext.Http.Method ?? "UNKNOWN";
    var path = request.RequestContext.Http.Path ?? "/";
    var routeKey = request.RequestContext.RouteKey; // e.g., "GET /badges/{id}"
    var route = routeKey?.IndexOf(' ', StringComparison.OrdinalIgnoreCase) is { } idx and >= 0 ? routeKey[(idx + 1)..] : null;

    // Add comprehensive HTTP context to our unified operation
    operation.AddTag("http.method", httpMethod)
        .AddTag("http.path", path)
        .AddTag("http.route", route ?? path)
        .AddTag("request.id", context.AwsRequestId)
        .SetDisplayName($"{httpMethod} {route ?? path}");

    // Add headers and stage information
    if (request.Headers?.TryGetValue("host", out var hostHeader) == true)
    {
        operation.AddTag("server.address", hostHeader);
    }

    if (request.Headers?.TryGetValue("x-forwarded-proto", out var proto) == true)
    {
        operation.AddTag("url.scheme", proto);
    }

    if (request.RequestContext?.Stage is { } stage)
    {
        operation.AddTag("server.port", string.Equals(stage, "$default", StringComparison.OrdinalIgnoreCase) ? null : stage);
    }

    return (httpMethod, path);
}
