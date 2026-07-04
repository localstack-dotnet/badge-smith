using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Api.Tests.Testing.Infrastructure;
using Xunit;

namespace BadgeSmith.Api.Tests.Functional;

[Collection("aspire-contract")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Functional)]
public sealed class RoutingContractTests(AspireContractFixture stack)
{
    [Fact]
    public async Task UnknownRoute_Should_Return404()
    {
        var r = await stack.Api.InvokeAsync("GET", "/nope/nothing/here", ct: TestContext.Current.CancellationToken);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Head_Should_BeRoutedLikeGet()
    {
        var r = await stack.Api.InvokeAsync("HEAD", "/health", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, r.StatusCode);
    }

    [Fact]
    public async Task OptionsPreflight_Should_ReturnCorsHeaders()
    {
        var r = await stack.Api.InvokeAsync("OPTIONS", "/badges/packages/nuget/contracttest.pkg",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["origin"] = "https://example.com",
                ["access-control-request-method"] = "GET",
            },
            ct: TestContext.Current.CancellationToken);
        Assert.Equal(204, r.StatusCode);
        Assert.NotNull(r.Headers);
        Assert.Equal("*", r.Headers["Access-Control-Allow-Origin"]);
        Assert.Contains("GET", r.Headers["Access-Control-Allow-Methods"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Responses_Should_CarryCorsHeader()
    {
        var r = await stack.Api.InvokeAsync("GET", "/health", ct: TestContext.Current.CancellationToken);
        Assert.NotNull(r.Headers);
        Assert.Equal("*", r.Headers["Access-Control-Allow-Origin"]);
    }
}
