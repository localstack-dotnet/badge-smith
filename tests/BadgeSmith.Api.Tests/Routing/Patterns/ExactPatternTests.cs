using BadgeSmith.Api.Core.Routing.Patterns;
using BadgeSmith.Api.Tests.TestHelpers;
using Xunit;

namespace BadgeSmith.Api.Tests.Routing.Patterns;

public sealed class ExactPatternTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/tests/results")]
    [InlineData("/status")]
    [InlineData("/api/v1/endpoint")]
    public void Constructor_Should_StoreLiteralCorrectly(string literal)
    {
        var pattern = new ExactPattern(literal);

        Assert.Equal(literal, pattern.Literal);
    }

    [Theory]
    [InlineData("/health", "/health", true)]
    [InlineData("/health", "/Health", true)] // Case insensitive
    [InlineData("/health", "/HEALTH", true)] // Case insensitive
    [InlineData("/tests/results", "/tests/results", true)]
    [InlineData("/tests/results", "/Tests/Results", true)] // Case insensitive
    [InlineData("/health", "/healthz", false)]
    [InlineData("/health", "/health/check", false)]
    [InlineData("/health", "/api/health", false)]
    [InlineData("/health", "", false)]
    [InlineData("/tests/results", "/tests", false)]
    [InlineData("/tests/results", "/results", false)]
    public void TryMatch_Should_HandleCaseInsensitiveMatching(string literal, string path, bool expectedMatch)
    {
        var pattern = RouteTestBuilder.CreateExactPattern(literal);
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.Equal(expectedMatch, result);
    }

    [Fact]
    public void TryMatch_Should_ReturnTrueForExactHealthMatch()
    {
        var pattern = RouteTestBuilder.CreateExactPattern("/health");
        const string path = "/health";
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.True(result);
    }

    [Fact]
    public void TryMatch_Should_ReturnTrueForTestResultsMatch()
    {
        var pattern = RouteTestBuilder.CreateExactPattern("/tests/results");
        const string path = "/tests/results";
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.True(result);
    }

    [Theory]
    [InlineData("/health", "/healthcheck")]
    [InlineData("/health", "/health/")]
    [InlineData("/health", "health")] // Missing leading slash
    [InlineData("/tests/results", "/tests/results/")]
    [InlineData("/tests/results", "/tests/result")]
    [InlineData("/tests/results", "/test/results")]
    public void TryMatch_Should_ReturnFalseForNonExactMatches(string literal, string path)
    {
        var pattern = RouteTestBuilder.CreateExactPattern(literal);
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.False(result);
    }

    [Fact]
    public void TryMatch_Should_NotModifyRouteValues()
    {
        var pattern = RouteTestBuilder.CreateExactPattern("/health");
        const string path = "/health";
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.True(result);

        // ExactPattern should not set any parameters
        var parameters = values.ToDictionary();
        Assert.Empty(parameters);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/tests/results")]
    [InlineData("/api/v1/status")]
    [InlineData("/badges/clear-cache")]
    public void TryMatch_Should_WorkWithVariousExactPaths(string exactPath)
    {
        var pattern = RouteTestBuilder.CreateExactPattern(exactPath);
        var values = RouteTestBuilder.CreateRouteValues(exactPath);

        var result = pattern.TryMatch(exactPath.AsSpan(), ref values);

        Assert.True(result);
    }

    [Theory]
    [InlineData("/health-check")]
    [InlineData("/health_check")]
    [InlineData("/health.check")]
    [InlineData("/health/check")]
    [InlineData("/api/health")]
    [InlineData("health")] // Missing leading slash (from formatting test)
    [InlineData("/health/")] // Extra trailing slash (from formatting test)
    [InlineData(" /health")] // Leading space (from formatting test)
    [InlineData("/health ")] // Trailing space (from formatting test)
    [InlineData("/tests//results")] // Double slash (from formatting test)
    [InlineData("")] // Empty string
    [InlineData(" ")] // Space
    [InlineData("\t")] // Tab
    [InlineData("\n")] // Newline
    public void TryMatch_Should_NotMatchSimilarOrMalformedPaths(string path)
    {
        var pattern = RouteTestBuilder.CreateExactPattern("/health");
        var values = RouteTestBuilder.CreateRouteValues(path);

        var result = pattern.TryMatch(path.AsSpan(), ref values);

        Assert.False(result);
    }

    [Theory]
    [MemberData(nameof(GetRealWorldExactPatterns))]
    public void TryMatch_Should_HandleRealWorldExactPatterns(string literal, string testPath, bool shouldMatch)
    {
        var pattern = RouteTestBuilder.CreateExactPattern(literal);
        var values = RouteTestBuilder.CreateRouteValues(testPath);

        var result = pattern.TryMatch(testPath.AsSpan(), ref values);

        Assert.Equal(shouldMatch, result);
    }

    public static IEnumerable<object[]> GetRealWorldExactPatterns()
    {
        // Health endpoint tests
        yield return ["/health", "/health", true];
        yield return ["/health", "/Health", true];
        yield return ["/health", "/HEALTH", true];
        yield return ["/health", "/healthz", false];
        yield return ["/health", "/health/check", false];

        // Test ingestion endpoint tests
        yield return ["/tests/results", "/tests/results", true];
        yield return ["/tests/results", "/Tests/Results", true];
        yield return ["/tests/results", "/TESTS/RESULTS", true];
        yield return ["/tests/results", "/tests/result", false];
        yield return ["/tests/results", "/test/results", false];
        yield return ["/tests/results", "/tests/results/", false];

        // Other potential exact endpoints
        yield return ["/status", "/status", true];
        yield return ["/status", "/Status", true];
        yield return ["/status", "/statuses", false];

        yield return ["/ping", "/ping", true];
        yield return ["/ping", "/Ping", true];
        yield return ["/ping", "/pong", false];

        // Edge cases
        yield return ["/", "/", true];
        yield return ["/", "", false];
        yield return ["", "", true];
        yield return ["", "/", false];
    }
}
