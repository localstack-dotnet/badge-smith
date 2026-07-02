using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Api.Tests.Testing.Infrastructure;
using Xunit;

namespace BadgeSmith.Api.Tests.Functional;

[Collection("contract")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Functional)]
[Trait("Category", TestCategories.AotContract)]
public sealed class PackageBadgeContractTests(BadgeSmithStackFixture stack)
{
    [Fact]
    public async Task NuGetBadge_Should_ReturnHighestStable()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("\"message\":\"13.0.3\"", r.Body, StringComparison.Ordinal);
        Assert.Contains("\"color\":\"blue\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NuGetBadge_WithPrerelease_Should_ReturnPrereleaseVersion()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg?prerelease=true", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("13.0.4-beta1", r.Body, StringComparison.Ordinal);
        Assert.Contains("\"color\":\"orange\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NuGetBadge_UnknownPackage_Should_Return404()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/nuget/missing.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task NuGetBadge_InvalidVersionRange_Should_Return400()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg?version=not-a-range", ct: TestContext.Current.CancellationToken);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task NuGetBadge_Should_Honor_IfNoneMatch()
    {
        var first = await stack.Lambda.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, first.StatusCode);
        var second = await stack.Lambda.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["if-none-match"] = first.Headers!["ETag"] },
            ct: TestContext.Current.CancellationToken);
        Assert.Equal(304, second.StatusCode);
    }

    [Fact]
    public async Task GitHubBadge_Should_ReturnHighestStable()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/github/test-org/contracttest.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("\"message\":\"2.1.0\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHubBadge_OrgWithoutSecret_Should_Return401()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/github/unknown-org/some.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(401, r.StatusCode);
    }

    [Fact]
    public async Task PackagesRoute_UnknownProvider_Should_Return400()
    {
        var r = await stack.Lambda.InvokeAsync("GET", "/badges/packages/npm/some.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(400, r.StatusCode);
    }
}
