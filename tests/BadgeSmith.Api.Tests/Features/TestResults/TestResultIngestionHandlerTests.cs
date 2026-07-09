using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Security.Contracts;
using BadgeSmith.Api.Features.TestResults.Contracts;
using BadgeSmith.Api.Features.TestResults.Handlers;
using BadgeSmith.Api.Tests.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Features.TestResults;

[Trait("Category", TestCategories.Unit)]
public sealed class TestResultIngestionHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_Return_Safe_BadRequest_Body_When_Body_Is_Invalid_Json()
    {
        var sut = new TestResultIngestionHandler(
            Mock.Of<ILogger<TestResultIngestionHandler>>(),
            Mock.Of<IHmacAuthenticationService>(),
            Mock.Of<ITestResultsService>());

        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = "{invalid json payload",
        };

        var routeContext = new RouteContext(
            request,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["owner"] = "owner",
                ["repo"] = "repo",
                ["platform"] = "linux",
                ["branch"] = "main",
            });

        var response = await sut.HandleAsync(routeContext, TestContext.Current.CancellationToken);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Invalid JSON payload", response.Body);
    }
}
