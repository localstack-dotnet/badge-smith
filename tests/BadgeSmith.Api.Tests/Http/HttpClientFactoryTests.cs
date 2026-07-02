using BadgeSmith.Api.Core.Http;
using Xunit;

namespace BadgeSmith.Api.Tests.Http;

public sealed class HttpClientFactoryTests : IDisposable
{
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", null);
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", null);
    }

    [Fact]
    public void CreateNuGetClient_Should_UseDefaultBaseAddress_WhenEnvNotSet()
    {
        using var client = HttpClientFactory.CreateNuGetClient();
        Assert.Equal(new Uri("https://api.nuget.org/"), client.BaseAddress);
    }

    [Fact]
    public void CreateNuGetClient_Should_UseEnvOverride_WhenSet()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", "http://wiremock:8080/nuget/");
        using var client = HttpClientFactory.CreateNuGetClient();
        Assert.Equal(new Uri("http://wiremock:8080/nuget/"), client.BaseAddress);
    }

    [Fact]
    public void CreateGithubClient_Should_UseEnvOverride_WhenSet()
    {
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", "http://wiremock:8080/github/");
        using var client = HttpClientFactory.CreateGithubClient();
        Assert.Equal(new Uri("http://wiremock:8080/github/"), client.BaseAddress);
    }

    [Fact]
    public void CreateNuGetClient_Should_FallBackToDefault_WhenEnvInvalid()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", "not-a-uri");
        using var client = HttpClientFactory.CreateNuGetClient();
        Assert.Equal(new Uri("https://api.nuget.org/"), client.BaseAddress);
    }

    [Fact]
    public void CreateNuGetClient_Should_NormalizeTrailingSlash_WhenEnvOverrideMissingTrailingSlash()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", "http://wiremock:8080/nuget");
        using var client = HttpClientFactory.CreateNuGetClient();
        Assert.Equal(new Uri("http://wiremock:8080/nuget/"), client.BaseAddress);
    }

    [Fact]
    public void CreateGithubClient_Should_NormalizeTrailingSlash_WhenEnvOverrideMissingTrailingSlash()
    {
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", "http://wiremock:8080/github");
        using var client = HttpClientFactory.CreateGithubClient();
        Assert.Equal(new Uri("http://wiremock:8080/github/"), client.BaseAddress);
    }

    [Fact]
    public void CreateGithubClient_Should_UseDefaultBaseAddress_WhenEnvNotSet()
    {
        using var client = HttpClientFactory.CreateGithubClient();
        Assert.Equal(new Uri("https://api.github.com/"), client.BaseAddress);
    }

    [Fact]
    public void CreateGithubClient_Should_FallBackToDefault_WhenEnvInvalid()
    {
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", "not-a-uri");
        using var client = HttpClientFactory.CreateGithubClient();
        Assert.Equal(new Uri("https://api.github.com/"), client.BaseAddress);
    }
}
