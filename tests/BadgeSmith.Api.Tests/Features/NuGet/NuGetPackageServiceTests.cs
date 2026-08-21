using System.Net;
using System.Text.Json;
using BadgeSmith.Api.Core.Versioning;
using BadgeSmith.Api.Features.NuGet;
using BadgeSmith.Api.Tests.TestHelpers;
using BadgeSmith.Api.Tests.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Features.NuGet;

[Trait("Category", TestCategories.Unit)]
public sealed class NuGetPackageServiceTests
{
    private const string OkIndexPayload = """{"versions":["1.0.0","1.2.0","2.0.0-preview"]}""";

    [Theory]
    [MemberData(nameof(UpstreamFetchMatrix.ScenarioNames), MemberType = typeof(UpstreamFetchMatrix))]
    public async Task GetLatestVersionAsync_Should_Uphold_Shared_Upstream_Matrix(string scenario)
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nuget.org/") };
        var cache = new RecordingAppCache();
        var sut = new NuGetPackageService(
            new NuGetVersionService(),
            Mock.Of<ILogger<NuGetPackageService>>(),
            httpClient,
            cache);
        var fixture = new UpstreamFetchMatrix.Fixture(
            ExpectedCacheKey: "nuget:index:newtonsoft.json",
            OkPayload: OkIndexPayload,
            ExpectedVersion: "1.2.0");

        await UpstreamFetchMatrix.RunAsync(
            scenario,
            handler,
            cache,
            fixture,
            async ct => ToOutcome(await sut.GetLatestVersionAsync("Newtonsoft.Json", ct: ct)));
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Normalize_And_Escape_Request_Path_When_PackageId_Requires_It()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nuget.org/") };
        var sut = new NuGetPackageService(
            new NuGetVersionService(),
            Mock.Of<ILogger<NuGetPackageService>>(),
            httpClient,
            new RecordingAppCache());
        handler.Respond(HttpStatusCode.OK, OkIndexPayload);

        _ = await sut.GetLatestVersionAsync("Fake Package", ct: TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.nuget.org/v3-flatcontainer/fake%20package/index.json", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Return_NotFound_When_Upstream_Returns_404()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nuget.org/") };
        var sut = new NuGetPackageService(
            new NuGetVersionService(),
            Mock.Of<ILogger<NuGetPackageService>>(),
            httpClient,
            new RecordingAppCache());
        handler.Respond(HttpStatusCode.NotFound, string.Empty);

        var result = await sut.GetLatestVersionAsync("ghost-package", ct: TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("Package 'ghost-package' not found", ExtractFailureReason(result));
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Return_NuGet_Labelled_Error_When_Upstream_Fails()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nuget.org/") };
        var sut = new NuGetPackageService(
            new NuGetVersionService(),
            Mock.Of<ILogger<NuGetPackageService>>(),
            httpClient,
            new RecordingAppCache());
        handler.Respond(HttpStatusCode.ServiceUnavailable, string.Empty);

        var result = await sut.GetLatestVersionAsync("some-package", ct: TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("NuGet API error: ServiceUnavailable", ExtractFailureReason(result));
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Return_NotFound_When_Index_Has_No_Versions()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nuget.org/") };
        var sut = new NuGetPackageService(
            new NuGetVersionService(),
            Mock.Of<ILogger<NuGetPackageService>>(),
            httpClient,
            new RecordingAppCache());
        handler.Respond(HttpStatusCode.OK, """{"versions":[]}""");

        var result = await sut.GetLatestVersionAsync("empty-package", ct: TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("No versions found for package 'empty-package'", ExtractFailureReason(result));
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Map_Prerelease_Version_When_Prerelease_Is_Requested()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nuget.org/") };
        var sut = new NuGetPackageService(
            new NuGetVersionService(),
            Mock.Of<ILogger<NuGetPackageService>>(),
            httpClient,
            new RecordingAppCache());
        handler.Respond(HttpStatusCode.OK, OkIndexPayload);

        var result = await sut.GetLatestVersionAsync("newtonsoft.json", includePrerelease: true, ct: TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("2.0.0-preview", result.NuGetPackageInfo!.VersionString);
        Assert.True(result.NuGetPackageInfo.IsPrerelease);
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Throw_When_Upstream_Payload_Is_Invalid_Json()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nuget.org/") };
        var sut = new NuGetPackageService(
            new NuGetVersionService(),
            Mock.Of<ILogger<NuGetPackageService>>(),
            httpClient,
            new RecordingAppCache());
        handler.Respond(HttpStatusCode.OK, "<definitely-not-json>");

        await Assert.ThrowsAsync<JsonException>(() => sut.GetLatestVersionAsync("broken-package", ct: TestContext.Current.CancellationToken));
    }

    private static UpstreamFetchMatrix.Outcome ToOutcome(NuGetResults result)
    {
        return result.IsSuccess
            ? new UpstreamFetchMatrix.Outcome(true, result.NuGetPackageInfo!.VersionString, result.NuGetPackageInfo.LastModifiedUtc, null)
            : new UpstreamFetchMatrix.Outcome(false, null, null, ExtractFailureReason(result));
    }

    private static string ExtractFailureReason(NuGetResults result) =>
        result.TryGetFailure(out var failure)
            ? failure!.Value.Match(
                notFound => notFound.Reason,
                validation => validation.Reason,
                error => error.Reason)
            : "unknown failure";
}
