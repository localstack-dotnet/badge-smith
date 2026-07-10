using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Tools.Infrastructure;
using Xunit;

namespace BadgeSmith.Api.Tests.Tooling;

[Trait("Category", TestCategories.Unit)]
public sealed class BadgeSmithUrlBuilderTests
{
    [Fact]
    public void Build_Urls_Should_Preserve_Port_And_Path_Prefix_And_Encode_Each_Route_Value()
    {
        var created = BadgeSmithUrlBuilder.TryCreate(
            "http://localhost:9474/prefix/",
            out var urls,
            out var error);

        Assert.True(created, error);
        Assert.Equal(
            "http://localhost:9474/prefix/tests/results/linux/localstack-dotnet/badge%20smith/feature%2Ftools",
            urls.BuildIngestUrl("linux", "localstack-dotnet", "badge smith", "feature/tools"));
        Assert.Equal(
            "http://localhost:9474/prefix/badges/tests/linux/localstack-dotnet/badge%20smith/feature%2Ftools",
            urls.BuildBadgeUrl("linux", "localstack-dotnet", "badge smith", "feature/tools"));
        Assert.Equal(
            "http://localhost:9474/prefix/redirect/test-results/linux/localstack-dotnet/badge%20smith/feature%2Ftools",
            urls.BuildRedirectUrl("linux", "localstack-dotnet", "badge smith", "feature/tools"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("api.example.com")]
    [InlineData("ftp://api.example.com")]
    [InlineData("https://user:password@api.example.com")]
    [InlineData("https://api.example.com?tenant=one")]
    [InlineData("https://api.example.com#badge")]
    public void TryCreate_Should_Reject_Unsafe_Base_Url(string baseUrl)
    {
        var created = BadgeSmithUrlBuilder.TryCreate(baseUrl, out _, out var error);

        Assert.False(created);
        Assert.Contains("Base URL", error, StringComparison.Ordinal);
    }
}
