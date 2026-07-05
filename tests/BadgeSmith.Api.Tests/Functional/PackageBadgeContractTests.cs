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
    public async Task NuGetBadge_Should_ReturnHighestStable()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("\"message\":\"13.0.3\"", r.Body, StringComparison.Ordinal);
        Assert.Contains("\"color\":\"blue\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NuGetBadge_WithPrerelease_Should_ReturnPrereleaseVersion()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg?prerelease=true", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("13.0.4-beta1", r.Body, StringComparison.Ordinal);
        Assert.Contains("\"color\":\"orange\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NuGetBadge_UnknownPackage_Should_Return404()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/missing.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task NuGetBadge_InvalidVersionRange_Should_Return400()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg?version=not-a-range", ct: TestContext.Current.CancellationToken);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task NuGetBadge_Should_Honor_IfNoneMatch()
    {
        var first = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, first.StatusCode);
        var second = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["if-none-match"] = first.Headers!["ETag"] },
            ct: TestContext.Current.CancellationToken);
        Assert.Equal(304, second.StatusCode);
    }

    [Fact]
    public async Task NuGetBadge_WithValidVersionRange_Should_ReturnMatchingVersion()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/nuget/contracttest.pkg?version=%5B4.0.0%2C5.0.0%29", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("\"message\":\"4.0.2\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHubBadge_Should_ReturnHighestStable()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/contracttest.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, r.StatusCode);
        Assert.Contains("\"message\":\"2.1.0\"", r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHubBadge_UpstreamUnauthorized_Should_Return401()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/unauthorized-org/any.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(401, r.StatusCode);
    }

    [Fact]
    public async Task GitHubBadge_UpstreamForbidden_Should_Return403()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/forbidden-org/any.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(403, r.StatusCode);
    }

    [Fact]
    public async Task GitHubBadge_UpstreamMissingPackage_Should_Return404()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/missing.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task GitHubBadge_UpstreamEmptyVersions_Should_Return404()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/empty.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task GitHubBadge_Should_Honor_IfNoneMatch()
    {
        var first = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/contracttest.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(200, first.StatusCode);
        var second = await stack.Api.InvokeAsync("GET", "/badges/packages/github/test-org/contracttest.pkg",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["if-none-match"] = first.Headers!["ETag"] },
            ct: TestContext.Current.CancellationToken);
        Assert.Equal(304, second.StatusCode);
    }

    [Fact]
    public async Task GitHubBadge_OrgWithoutSecret_Should_Return401()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/github/unknown-org/some.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(401, r.StatusCode);
    }

    [Fact]
    public async Task PackagesRoute_UnknownProvider_Should_Return400()
    {
        var r = await stack.Api.InvokeAsync("GET", "/badges/packages/npm/some.pkg", ct: TestContext.Current.CancellationToken);
        Assert.Equal(400, r.StatusCode);
    }
}
