using BadgeSmith.Api.Core.Http;
using BadgeSmith.Api.Tests.Testing;
using Xunit;
using static BadgeSmith.Constants;

namespace BadgeSmith.Api.Tests.Http;

[Trait("Category", TestCategories.Unit)]
public sealed class HttpClientFactoryTests : IDisposable
{
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", null);
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", null);
        Environment.SetEnvironmentVariable(UpstreamModeEnvironmentVariable, null);
    }

    [Fact]
    public void CreateNuGetClient_Should_Use_Default_BaseAddress_When_Environment_Variable_Is_Not_Set()
    {
        using var client = HttpClientFactory.CreateNuGetClient();
        Assert.Equal(new Uri("https://api.nuget.org/"), client.BaseAddress);
    }

    [Fact]
    public void CreateNuGetClient_Should_Use_Environment_Override_When_Set()
    {
        SetMockUpstreams();
        using var client = HttpClientFactory.CreateNuGetClient();
        Assert.Equal(new Uri("http://wiremock:8080/nuget/"), client.BaseAddress);
    }

    [Fact]
    public void CreateGithubClient_Should_Use_Environment_Override_When_Set()
    {
        SetMockUpstreams();
        using var client = HttpClientFactory.CreateGithubClient();
        Assert.Equal(new Uri("http://wiremock:8080/github/"), client.BaseAddress);
    }

    [Fact]
    public void CreateNuGetClient_Should_Reject_Environment_Override_When_Invalid()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", "not-a-uri");

        var exception = Assert.Throws<InvalidOperationException>(HttpClientFactory.CreateNuGetClient);

        Assert.Contains("HTTP_NUGET_BASE_URL", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateNuGetClient_Should_Normalize_Trailing_Slash_When_Environment_Override_Is_Missing_Trailing_Slash()
    {
        SetMockUpstreams();
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", "http://wiremock:8080/nuget");
        using var client = HttpClientFactory.CreateNuGetClient();
        Assert.Equal(new Uri("http://wiremock:8080/nuget/"), client.BaseAddress);
    }

    [Fact]
    public void CreateGithubClient_Should_Normalize_Trailing_Slash_When_Environment_Override_Is_Missing_Trailing_Slash()
    {
        SetMockUpstreams();
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", "http://wiremock:8080/github");
        using var client = HttpClientFactory.CreateGithubClient();
        Assert.Equal(new Uri("http://wiremock:8080/github/"), client.BaseAddress);
    }

    [Fact]
    public void CreateGithubClient_Should_Use_Default_BaseAddress_When_Environment_Variable_Is_Not_Set()
    {
        using var client = HttpClientFactory.CreateGithubClient();
        Assert.Equal(new Uri("https://api.github.com/"), client.BaseAddress);
    }

    [Fact]
    public void CreateGithubClient_Should_Reject_Environment_Override_When_Invalid()
    {
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", "not-a-uri");

        var exception = Assert.Throws<InvalidOperationException>(HttpClientFactory.CreateGithubClient);

        Assert.Contains("HTTP_GITHUB_BASE_URL", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateNuGetClient_Should_Reject_Public_Http_When_Mode_Is_Live()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", "http://api.example.com/nuget/");

        var exception = Assert.Throws<InvalidOperationException>(HttpClientFactory.CreateNuGetClient);

        Assert.Contains("HTTPS", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateNuGetClient_Should_Require_Both_Overrides_When_Mode_Is_Mock()
    {
        Environment.SetEnvironmentVariable(UpstreamModeEnvironmentVariable, UpstreamModeMock);
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", "http://wiremock:8080/nuget/");

        var exception = Assert.Throws<InvalidOperationException>(HttpClientFactory.CreateNuGetClient);

        Assert.Contains("both", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateNuGetClient_Should_Reject_Invalid_Upstream_Mode()
    {
        Environment.SetEnvironmentVariable(UpstreamModeEnvironmentVariable, "invalid");

        var exception = Assert.Throws<InvalidOperationException>(HttpClientFactory.CreateNuGetClient);

        Assert.Contains(UpstreamModeEnvironmentVariable, exception.Message, StringComparison.Ordinal);
    }

    private static void SetMockUpstreams()
    {
        Environment.SetEnvironmentVariable(UpstreamModeEnvironmentVariable, UpstreamModeMock);
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", "http://wiremock:8080/nuget/");
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", "http://wiremock:8080/github/");
    }
}
