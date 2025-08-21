using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using BadgeSmith;
using BadgeSmith.Api.Extensions;
using BadgeSmith.Api.Json;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder();
builder.AddServiceDefaults();
builder.Services.AddBadgeSmithApi();

var host = builder.Build();

var traceProvider = host.Services.GetRequiredService<TracerProvider>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var apiRouter = host.Services.GetRequiredService<IApiRouter>();

var handler = FunctionHandlerAsync;

await LambdaBootstrapBuilder.Create(handler, new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>())
    .Build()
    .RunAsync()
    .ConfigureAwait(false);

return;

async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandlerAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
{
    var httpMethod = request.RequestContext.Http.Method;
    var path = request.RequestContext.Http.Path;
    using var main = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{httpMethod} {path}");

    return await AWSLambdaWrapper.TraceAsync(traceProvider, async (req, ctx) =>
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity(nameof(FunctionHandlerAsync));

        using var contextLog = request.PushApiGatewayContext(includeHeaders: true);
        using var scope = logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["AwsRequestId"] = context.AwsRequestId,
            ["FunctionName"] = context.FunctionName,
            ["RemainingTimeMs"] = context.RemainingTime.TotalMilliseconds,
        });

        logger.LogInformation("Handling {Method} {Path}", request.RequestContext.Http.Method, request.RequestContext.Http.Path);

        try
        {
            return await apiRouter.RouteAsync(req, host.Services).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            ctx.Logger.LogError($"Error processing request: {ex}");
            return ResponseHelper.InternalServerError("An error occurred processing the request");
        }
    }, request, context, main?.Context ?? default).ConfigureAwait(false);
}
