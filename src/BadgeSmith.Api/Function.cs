using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BadgeSmith.Api;

internal static class Function
{
    private static async Task Main()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddScoped<Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>>>(_ => FunctionHandlerAsync);

        // var handler = FunctionHandlerAsync;

        await LambdaBootstrapBuilder.Create(handler, new SourceGeneratorLambdaJsonSerializer<Json.LambdaFunctionJsonSerializerContext>())
            .Build()
            .RunAsync()
            .ConfigureAwait(false);
    }

    internal static Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandlerAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        return Task.FromResult(Ok());
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

    private static APIGatewayHttpApiV2ProxyResponse Ok(string? responseBody = null) => new()
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
}
