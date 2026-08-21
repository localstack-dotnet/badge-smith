#if ENABLE_TELEMETRY
using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using BadgeSmith;
using BadgeSmith.Api.Core;
using BadgeSmith.Api.Core.Observability;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;
using static BadgeSmith.Api.Core.Settings;
using LambdaFunctionJsonSerializerContext = BadgeSmith.Api.Core.Infrastructure.LambdaFunctionJsonSerializerContext;
using LoggerFactory = BadgeSmith.Api.Core.Observability.LoggerFactory;

using var tracerProvider = TelemetryFactory.CreateTracerProvider(ApplicationName, ApplicationVersion);
LambdaBootstrap lambdaBootstrap;
using (var _ = BadgeSmithApiActivitySource.ActivitySource.StartActivity("Lambda Initialization"))
{
    var apiRouter = ApplicationRegistry.ApiRouter;
    var logger = LoggerFactory.CreateLogger<Program>();
    var processor = new LambdaRequestProcessor(apiRouter, logger);
    var handler = BuildHandler(tracerProvider, processor);

    var jsonSerializer = new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>();
    lambdaBootstrap = LambdaBootstrapBuilder.Create(handler, jsonSerializer).Build();
}

await lambdaBootstrap.RunAsync().ConfigureAwait(false);

return;

static Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>> BuildHandler(
    TracerProvider tracerProvider,
    LambdaRequestProcessor processor)
{
    return (req, ctx) =>
        AWSLambdaWrapper.TraceAsync(
            tracerProvider,
            (wrappedReq, wrappedCtx) =>
            {
                SetHttpTags(wrappedReq, wrappedCtx);
                return processor.HandleAsync(wrappedReq, wrappedCtx);
            },
            req, ctx);
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

    if (Activity.Current is { } a)
    {
        a.DisplayName = $"{httpMethod} {route ?? path}";
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
