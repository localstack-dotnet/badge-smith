using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Routing;
using BadgeSmith.Api.Routing.Contracts;
using BadgeSmith.Api.Routing.Helpers;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Handlers;

internal interface ITestResultIngestionHandler : IRouteHandler;

internal class TestResultIngestionHandler : ITestResultIngestionHandler
{
    private readonly ILogger<TestResultIngestionHandler> _logger;

    public TestResultIngestionHandler(ILogger<TestResultIngestionHandler> logger)
    {
        _logger = logger;
    }

    public Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(RouteContext context, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultIngestionHandler)}.{nameof(HandleAsync)}");

        _logger.LogInformation("Test result ingest badge request received");

        return Task.FromResult(ResponseHelper.Redirect(
            location: "https://github.com/localstack-dotnet/localstack-dotnet-client/runs/46719603897",
            cacheControl: "public, max-age=60"));
    }
}
