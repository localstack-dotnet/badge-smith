using BadgeSmith.Api.Core.Http;
using BadgeSmith.Api.Tests.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Http;

[Trait("Category", TestCategories.Unit)]
public sealed class HttpClientFactoryTests : IDisposable
{
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", null);
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", null);
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
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", "http://wiremock:8080/nuget/");
        using var client = HttpClientFactory.CreateNuGetClient();
        Assert.Equal(new Uri("http://wiremock:8080/nuget/"), client.BaseAddress);
    }

    [Fact]
    public void CreateGithubClient_Should_Use_Environment_Override_When_Set()
    {
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", "http://wiremock:8080/github/");
        using var client = HttpClientFactory.CreateGithubClient();
        Assert.Equal(new Uri("http://wiremock:8080/github/"), client.BaseAddress);
    }

    [Fact]
    public void CreateNuGetClient_Should_Fall_Back_To_Default_When_Environment_Variable_Is_Invalid()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", "not-a-uri");
        using var client = HttpClientFactory.CreateNuGetClient();
        Assert.Equal(new Uri("https://api.nuget.org/"), client.BaseAddress);
    }

    [Fact]
    public void CreateNuGetClient_Should_Normalize_Trailing_Slash_When_Environment_Override_Is_Missing_Trailing_Slash()
    {
        Environment.SetEnvironmentVariable("HTTP_NUGET_BASE_URL", "http://wiremock:8080/nuget");
        using var client = HttpClientFactory.CreateNuGetClient();
        Assert.Equal(new Uri("http://wiremock:8080/nuget/"), client.BaseAddress);
    }

    [Fact]
    public void CreateGithubClient_Should_Normalize_Trailing_Slash_When_Environment_Override_Is_Missing_Trailing_Slash()
    {
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
    public void CreateGithubClient_Should_Fall_Back_To_Default_When_Environment_Variable_Is_Invalid()
    {
        Environment.SetEnvironmentVariable("HTTP_GITHUB_BASE_URL", "not-a-uri");
        using var client = HttpClientFactory.CreateGithubClient();
        Assert.Equal(new Uri("https://api.github.com/"), client.BaseAddress);
    }
}
