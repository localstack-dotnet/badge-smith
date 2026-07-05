using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Api.Tests.Testing.Infrastructure;
using Xunit;

namespace BadgeSmith.Api.Tests.Functional;

[Collection("aspire-contract")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Functional)]
public sealed class PackageBadgeContractTests(AspireContractFixture stack)
{
    [Fact]
    public async Task NuGet_Badge_Should_Return_Highest_Stable()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("\"message\":\"13.0.3\"", r.Body, StringComparison.Ordinal);
        Assert.Contains("\"color\":\"blue\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NuGet_Badge_Should_Return_Prerelease_Version_When_Prerelease_Is_Requested()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg?prerelease=true", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("13.0.4-beta1", r.Body, StringComparison.Ordinal);
        Assert.Contains("\"color\":\"orange\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NuGet_Badge_Should_Return_404_When_Package_Is_Unknown()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/missing.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task NuGet_Badge_Should_Return_400_When_Version_Range_Is_Invalid()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg?version=not-a-range", ct: TestContext.Current.CancellationToken);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task NuGet_Badge_Should_Honor_IfNoneMatch()
    {
        var first = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, first.StatusCode);
        var second = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["if-none-match"] = first.Headers!["ETag"] },
            ct: TestContext.Current.CancellationToken);
        Assert.Equal(304, second.StatusCode);
    }

    [Fact]
    public async Task NuGet_Badge_Should_Return_Matching_Version_When_Version_Range_Is_Valid()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg?version=%5B4.0.0%2C5.0.0%29", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("\"message\":\"4.0.2\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_Badge_Should_Return_Highest_Stable()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/contracttest.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("\"message\":\"2.1.0\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_Badge_Should_Return_401_When_Upstream_Is_Unauthorized()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/unauthorized-org/any.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(401, r.StatusCode);
    }

    [Fact]
    public async Task GitHub_Badge_Should_Return_403_When_Upstream_Is_Forbidden()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/forbidden-org/any.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(403, r.StatusCode);
    }

    [Fact]
    public async Task GitHub_Badge_Should_Return_404_When_Upstream_Package_Is_Missing()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/missing.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task GitHub_Badge_Should_Return_404_When_Upstream_Versions_Are_Empty()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/empty.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task GitHub_Badge_Should_Honor_IfNoneMatch()
    {
        var first = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/contracttest.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, first.StatusCode);
        var second = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/contracttest.pkg",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["if-none-match"] = first.Headers!["ETag"] },
            ct: TestContext.Current.CancellationToken);
        Assert.Equal(304, second.StatusCode);
    }

    [Fact]
    public async Task GitHub_Badge_Should_Return_401_When_Org_Has_No_Secret()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/unknown-org/some.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(401, r.StatusCode);
    }

    [Fact]
    public async Task Packages_Route_Should_Return_400_When_Provider_Is_Unknown()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/npm/some.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(400, r.StatusCode);
    }
}
