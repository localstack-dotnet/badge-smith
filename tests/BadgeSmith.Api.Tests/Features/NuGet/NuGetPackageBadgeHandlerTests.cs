using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Features.NuGet;
using BadgeSmith.Api.Features.NuGet.Contracts;
using BadgeSmith.Api.Tests.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Features.NuGet;

[Trait("Category", TestCategories.Unit)]
public sealed class NuGetPackageBadgeHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_Return_Safe_ErrorResponse_Body_When_Upstream_304_Has_No_Cached_Entry()
    {
        var packageService = new Mock<INuGetPackageService>();
        packageService
            .Setup(service => service.GetLatestVersionAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NuGetResults(new Error("Received 304 Not Modified without a cached entry")));

        var sut = new NuGetPackageBadgeHandler(Mock.Of<ILogger<NuGetPackageBadgeHandler>>(), packageService.Object);

        var routeContext = new RouteContext(
            new APIGatewayHttpApiV2ProxyRequest { Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "nuget",
                ["package"] = "newtonsoft.json",
            });

        var response = await sut.HandleAsync(routeContext, TestContext.Current.CancellationToken);

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("""{"message":"Received 304 Not Modified without a cached entry"}""", response.Body);
        Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);
    }
}
