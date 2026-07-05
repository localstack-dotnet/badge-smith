using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Api.Tests.Testing.Infrastructure;
using Xunit;

namespace BadgeSmith.Api.Tests.Functional;

[Collection("aspire-contract")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Functional)]
public sealed class HealthContractTests(AspireContractFixture stack)
{
    [Fact]
    public async Task Health_Should_Return200_WithNoCacheHeaders()
    {
        var response = await stack.Api.InvokeAsync("GET", "/health", ct: TestContext.Current.CancellationToken);

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("Healthy", response.Body ?? string.Empty, StringComparison.Ordinal);
        Assert.NotNull(response.Headers);
        Assert.Contains("no-store", response.Headers["Cache-Control"], StringComparison.OrdinalIgnoreCase);
    }
}
