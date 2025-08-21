using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using BadgeSmith.Api.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder();
builder.AddServiceDefaults();

var host = builder.Build();

var traceProvider = host.Services.GetRequiredService<TracerProvider>();

var handler = FunctionHandlerAsync;

await LambdaBootstrapBuilder.Create(handler, new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>())
    .Build()
    .RunAsync()
    .ConfigureAwait(false);
return;

Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandlerAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
{
    return AWSLambdaWrapper.TraceAsync(traceProvider, (_, _) => Task.FromResult(Ok()), request, context);
}

// private static APIGatewayHttpApiV2ProxyResponse BadRequest(string msg) => new()
// {
//     StatusCode = (int)HttpStatusCode.BadRequest,
//     Body = JsonSerializer.Serialize(new
//     {
//         error = msg
//     }),
//     Headers = new Dictionary<string, string>
//         (StringComparer.Ordinal)
//         {
//             { "Content-Type", "application/json" },
//         },
// };

// private static APIGatewayHttpApiV2ProxyResponse Created(string responseBody) => new()
// {
//     StatusCode = (int)HttpStatusCode.Created,
//     Body = responseBody,
//     Headers = new Dictionary<string, string>(StringComparer.Ordinal)
//     {
//         ["Content-Type"] = "application/json",
//     },
// };

APIGatewayHttpApiV2ProxyResponse Ok(string? responseBody = null) => new()
{
    StatusCode = (int)HttpStatusCode.OK,
    Body = responseBody,
    Headers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Content-Type"] = "application/json",
    },
};

// private static APIGatewayHttpApiV2ProxyResponse NotFound() => new()
// {
//     StatusCode = (int)HttpStatusCode.NotFound,
//     Body = string.Empty,
// };
// }
