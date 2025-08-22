using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
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
    // Pull values up-front
    var httpMethod = request.RequestContext.Http.Method ?? "UNKNOWN";
    var path = request.RequestContext.Http.Path ?? "/";
    var routeKey = request.RequestContext.RouteKey; // e.g. "GET /badges/{id}"
    var route = routeKey?.IndexOf(' ', StringComparison.OrdinalIgnoreCase) is { } idx and >= 0 ? routeKey[(idx + 1)..] : null;

    return await AWSLambdaWrapper.TraceAsync(traceProvider, async (req, ctx) =>
    {
        var span = Activity.Current;

        // Rename it early so child spans inherit a good parent name
        if (span != null)
        {
            span.DisplayName = $"{httpMethod} {route ?? path}";
        }

        // Use stable HTTP semantic conventions
        span?.SetTag("http.request.method", httpMethod);
        span?.SetTag("url.path", path);
        if (!string.IsNullOrEmpty(route) && span != null)
        {
            span.SetTag("http.route", route);
        }

        // (optional) a few more useful, low-cardinality attrs
        if (request.Headers?.TryGetValue("host", out var hostHeader) == true && span != null)
        {
            span.SetTag("server.address", hostHeader);
        }

        if (request.Headers?.TryGetValue("x-forwarded-proto", out var proto) == true && span != null)
        {
            span.SetTag("url.scheme", proto);
        }

        if (request.RequestContext?.Stage is { } stage && span != null)
        {
            span.SetTag("server.port", string.Equals(stage, "$default", StringComparison.OrdinalIgnoreCase) ? null : stage); // or stash stage in a custom attr
        }

        using var contextLog = req.PushApiGatewayContext(includeHeaders: true);
        using var scope = logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["AwsRequestId"] = ctx.AwsRequestId,
            ["FunctionName"] = ctx.FunctionName,
            ["RemainingTimeMs"] = ctx.RemainingTime.TotalMilliseconds,
        });

        logger.LogInformation("Handling {Method} {Path}", httpMethod, path);

        APIGatewayHttpApiV2ProxyResponse? response = null;

        try
        {
            response = await apiRouter.RouteAsync(req, host.Services).ConfigureAwait(false);
            return response;
        }
        catch (Exception ex)
        {
            span?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Unhandled error");
            response = ResponseHelper.InternalServerError("An error occurred processing the request");
            return response;
        }
        finally
        {
            if (Activity.Current is { } s && response is not null)
            {
                s.SetTag("http.response.status_code", response.StatusCode);
            }
        }
    }, request, context).ConfigureAwait(false);
}
