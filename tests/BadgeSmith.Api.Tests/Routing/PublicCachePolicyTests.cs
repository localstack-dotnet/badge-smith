using BadgeSmith.Api.Core.Routing.Helpers;
using BadgeSmith.Api.Tests.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing;

[Trait("Category", TestCategories.Unit)]
public sealed class PublicCachePolicyTests
{
    [Fact]
    public void Constructor_Should_Precompute_Deterministic_CacheControl_When_Valid_Ttls_Are_Given()
    {
        var policy = new PublicCachePolicy(
            sharedMaxAge: TimeSpan.FromSeconds(600),
            clientMaxAge: TimeSpan.FromSeconds(300),
            staleWhileRevalidate: TimeSpan.FromSeconds(1200),
            staleIfError: TimeSpan.FromSeconds(3600));

        Assert.Equal("public, s-maxage=600, max-age=300, stale-while-revalidate=1200, stale-if-error=3600", policy.CacheControl);
        Assert.Equal(TimeSpan.FromSeconds(600), policy.SharedMaxAge);
        Assert.Equal(TimeSpan.FromSeconds(300), policy.ClientMaxAge);
        Assert.Equal(TimeSpan.FromSeconds(1200), policy.StaleWhileRevalidate);
        Assert.Equal(TimeSpan.FromSeconds(3600), policy.StaleIfError);
    }

    [Fact]
    public void CacheControl_Should_Be_Stable_Across_Instances_And_Reused_Per_Instance()
    {
        var first = new PublicCachePolicy(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(600));
        var second = new PublicCachePolicy(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(600));

        Assert.Same(first.CacheControl, first.CacheControl);
        Assert.Equal(first.CacheControl, second.CacheControl);
    }

    [Fact]
    public void Constructor_Should_Accept_Zero_Ttls()
    {
        var policy = new PublicCachePolicy(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);

        Assert.Equal("public, s-maxage=0, max-age=0, stale-while-revalidate=0, stale-if-error=0", policy.CacheControl);
    }

    [Fact]
    public void Negative_Should_Be_Rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PublicCachePolicy(TimeSpan.FromSeconds(-1), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));

    [Fact]
    public void SubSecond_Should_Be_Rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PublicCachePolicy(TimeSpan.FromMilliseconds(500), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));

    [Fact]
    public void Overflow_Should_Be_Rejected()
    {
        var beyondIntMaxSeconds = TimeSpan.FromSeconds((long)int.MaxValue + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new PublicCachePolicy(beyondIntMaxSeconds, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));
    }
}
