#if !ENABLE_TELEMETRY
#pragma warning disable CA1502

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;
using LoggerFactory = BadgeSmith.Api.Observability.LoggerFactory;

//using var initScope = PerfTracker.StartScope("Lambda Initialization", nameof(Program));

var apiRouter = ApiRouterBuilder.BuildApiRouter();
var handler = BuildHandler(apiRouter);

var jsonSerializer = new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>();
var lambdaBootstrap = LambdaBootstrapBuilder.Create(handler, jsonSerializer).Build();
//initScope.Dispose();

await lambdaBootstrap.RunAsync().ConfigureAwait(false);
return;

static Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>> BuildHandler(ApiRouter apiRouter)
{
    return (req, ctx) => FunctionCoreAsync(req, ctx, apiRouter);
}

static async Task<APIGatewayHttpApiV2ProxyResponse> FunctionCoreAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context, ApiRouter apiRouter)
{
    // using var perfScope = PerfTracker.StartScope(nameof(FunctionCoreAsync), typeof(Program).FullName);
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
#endif
