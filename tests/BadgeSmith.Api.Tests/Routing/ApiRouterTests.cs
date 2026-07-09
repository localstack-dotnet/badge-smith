using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Routing.Contracts;
using BadgeSmith.Api.Tests.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing;

[Trait("Category", TestCategories.Unit)]
public sealed class ApiRouterTests
{
    [Fact]
    public async Task RouteAsync_Should_Return_Generic_Error_Message_When_Handler_Throws()
    {
        const string secretLeak = "SECRET-INTERNAL-STACKTRACE-DETAILS";

        var corsHandler = new Mock<ICorsHandler>(MockBehavior.Strict);
        corsHandler
            .Setup(c => c.HandlePreflight(It.IsAny<IDictionary<string, string>?>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException(secretLeak));

        var sut = new ApiRouter(
            Mock.Of<ILogger<ApiRouter>>(),
            Mock.Of<IRouteResolver>(),
            corsHandler.Object);

        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                {
                    Method = "OPTIONS",
                    Path = "/anything",
                },
            },
        };

        var response = await sut.RouteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("An error occurred processing the request", response.Body);
        Assert.DoesNotContain(secretLeak, response.Body ?? string.Empty, StringComparison.Ordinal);
    }
}
