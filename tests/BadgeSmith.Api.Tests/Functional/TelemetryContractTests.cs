using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Api.Tests.Testing.Infrastructure;
using Xunit;

namespace BadgeSmith.Api.Tests.Functional;

[Collection("aspire-contract")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Functional)]
public sealed class TelemetryContractTests(AspireContractFixture stack)
{
    [Fact]
    public async Task Lambda_Should_Export_Telemetry_To_Test_Collector_When_Requests_Complete()
    {
        var ct = TestContext.Current.CancellationToken;
        var before = await stack.CountTelemetryExportsAsync(ct);

        var first = await stack.Api.InvokeAsync("GET", "/health", ct: ct);
        var second = await stack.Api.InvokeAsync("GET", "/health", ct: ct);

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        var after = await stack.CountTelemetryExportsAsync(ct);
        Assert.True(after >= before + 2, $"Expected exports for both completed requests; observed {after - before}.");
    }
}
