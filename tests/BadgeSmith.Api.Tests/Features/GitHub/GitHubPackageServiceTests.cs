using System.Net;
using System.Text.Json;
using BadgeSmith.Api.Core.Versioning;
using BadgeSmith.Api.Features.GitHub;
using BadgeSmith.Api.Tests.TestHelpers;
using BadgeSmith.Api.Tests.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Features.GitHub;

[Trait("Category", TestCategories.Unit)]
public sealed class GitHubPackageServiceTests
{
    private const string OkVersionsPayload = """[{"name":"1.0.0"},{"name":"1.2.0"},{"name":"2.0.0-preview"}]""";

    [Theory]
    [MemberData(nameof(UpstreamFetchMatrix.ScenarioNames), MemberType = typeof(UpstreamFetchMatrix))]
    public async Task GetLatestVersionAsync_Should_Uphold_Shared_Upstream_Matrix(string scenario)
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var cache = new RecordingAppCache();
        var sut = new GitHubPackageService(
            httpClient,
            new NuGetVersionService(),
            cache,
            Mock.Of<ILogger<GitHubPackageService>>());
        var fixture = new UpstreamFetchMatrix.Fixture(
            ExpectedCacheKey: "github_package:index:acme-org:widget",
            OkPayload: OkVersionsPayload,
            ExpectedVersion: "1.2.0");

        await UpstreamFetchMatrix.RunAsync(
            scenario,
            handler,
            cache,
            fixture,
            async ct => ToOutcome(await sut.GetLatestVersionAsync("acme-org", "widget", "token-123", ct: ct)));
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Send_Bearer_And_GitHub_Accept_Headers()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubPackageService(
            httpClient,
            new NuGetVersionService(),
            new RecordingAppCache(),
            Mock.Of<ILogger<GitHubPackageService>>());
        handler.Respond(HttpStatusCode.OK, OkVersionsPayload);

        _ = await sut.GetLatestVersionAsync("acme-org", "widget", "token-123", ct: TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer token-123", request.Headers.Authorization!.ToString());
        Assert.Contains(request.Headers.Accept, mediaType => mediaType.ToString() == "application/vnd.github+json");
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Normalize_And_Escape_Org_And_Package_Path_Segments()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubPackageService(
            httpClient,
            new NuGetVersionService(),
            new RecordingAppCache(),
            Mock.Of<ILogger<GitHubPackageService>>());
        handler.Respond(HttpStatusCode.OK, OkVersionsPayload);

        _ = await sut.GetLatestVersionAsync("Acme Org", "Widget Package", "token-123", ct: TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.github.com/orgs/acme%20org/packages/nuget/widget%20package/versions", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Return_NotFound_When_Upstream_Returns_404()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubPackageService(
            httpClient,
            new NuGetVersionService(),
            new RecordingAppCache(),
            Mock.Of<ILogger<GitHubPackageService>>());
        handler.Respond(HttpStatusCode.NotFound, string.Empty);

        var result = await sut.GetLatestVersionAsync("acme-org", "ghost-package", "token-123", ct: TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("notFound", ExtractFailureKind(result));
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Return_Unauthorized_When_Upstream_Returns_401()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubPackageService(
            httpClient,
            new NuGetVersionService(),
            new RecordingAppCache(),
            Mock.Of<ILogger<GitHubPackageService>>());
        handler.Respond(HttpStatusCode.Unauthorized, string.Empty);

        var result = await sut.GetLatestVersionAsync("acme-org", "widget", "expired-token", ct: TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("unauthorized", ExtractFailureKind(result));
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Return_Forbidden_When_Upstream_Returns_403()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubPackageService(
            httpClient,
            new NuGetVersionService(),
            new RecordingAppCache(),
            Mock.Of<ILogger<GitHubPackageService>>());
        handler.Respond(HttpStatusCode.Forbidden, string.Empty);

        var result = await sut.GetLatestVersionAsync("acme-org", "widget", "limited-token", ct: TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("forbidden", ExtractFailureKind(result));
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Return_GitHub_Labelled_Error_When_Upstream_Fails()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubPackageService(
            httpClient,
            new NuGetVersionService(),
            new RecordingAppCache(),
            Mock.Of<ILogger<GitHubPackageService>>());
        handler.Respond(HttpStatusCode.InternalServerError, string.Empty);

        var result = await sut.GetLatestVersionAsync("acme-org", "widget", "token-123", ct: TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("error", ExtractFailureKind(result));
        Assert.Equal("GitHub API error: InternalServerError", ExtractFailureReason(result));
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Map_Prerelease_Version_When_Prerelease_Is_Requested()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubPackageService(
            httpClient,
            new NuGetVersionService(),
            new RecordingAppCache(),
            Mock.Of<ILogger<GitHubPackageService>>());
        handler.Respond(HttpStatusCode.OK, OkVersionsPayload);

        var result = await sut.GetLatestVersionAsync("acme-org", "widget", "token-123", includePrerelease: true, ct: TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("2.0.0-preview", result.GitHubPackageInfo!.VersionString);
        Assert.True(result.GitHubPackageInfo.IsPrerelease);
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Return_NotFound_When_Versions_List_Is_Empty()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubPackageService(
            httpClient,
            new NuGetVersionService(),
            new RecordingAppCache(),
            Mock.Of<ILogger<GitHubPackageService>>());
        handler.Respond(HttpStatusCode.OK, "[]");

        var result = await sut.GetLatestVersionAsync("acme-org", "empty-package", "token-123", ct: TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("No versions found for package 'empty-package'", ExtractFailureReason(result));
    }

    [Fact]
    public async Task GetLatestVersionAsync_Should_Throw_When_Upstream_Payload_Is_Invalid_Json()
    {
        using var handler = new StubHttpHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var sut = new GitHubPackageService(
            httpClient,
            new NuGetVersionService(),
            new RecordingAppCache(),
            Mock.Of<ILogger<GitHubPackageService>>());
        handler.Respond(HttpStatusCode.OK, "<definitely-not-json>");

        await Assert.ThrowsAsync<JsonException>(() => sut.GetLatestVersionAsync("acme-org", "broken-package", "token-123", ct: TestContext.Current.CancellationToken));
    }

    private static UpstreamFetchMatrix.Outcome ToOutcome(GitHubPackageResult result)
    {
        return result.IsSuccess
            ? new UpstreamFetchMatrix.Outcome(true, result.GitHubPackageInfo!.VersionString, result.GitHubPackageInfo.LastModifiedUtc, null)
            : new UpstreamFetchMatrix.Outcome(false, null, null, ExtractFailureReason(result));
    }

    private static string ExtractFailureKind(GitHubPackageResult result) => result.Failure.Match(
        _ => "notFound",
        _ => "range",
        _ => "unauthorized",
        _ => "forbidden",
        _ => "error");

    private static string ExtractFailureReason(GitHubPackageResult result) => result.Failure.Match(
        notFound => notFound.Reason,
        range => range.Reason,
        unauthorized => unauthorized.Reason,
        forbidden => forbidden.Reason,
        error => error.Reason);
}
