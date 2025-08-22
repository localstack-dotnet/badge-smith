using System.Text.RegularExpressions;
using BadgeSmith.Api.Handlers;

namespace BadgeSmith.Api.Routing;

/// <summary>
/// Centralized route table containing all API endpoint definitions with source-generated regex patterns.
/// Uses compile-time regex generation for optimal performance and memory efficiency in AWS Lambda environments.
/// Routes are ordered from most specific to least specific to ensure correct pattern matching.
/// </summary>
internal static partial class RouteTable
{
    /// <summary>
    /// Common regex options for all route patterns: non-backtracking for security and case-insensitive matching.
    /// </summary>
    private const RegexOptions RouteOptions = RegexOptions.NonBacktracking | RegexOptions.IgnoreCase;

    /// <summary>
    /// Maximum time allowed for regex pattern matching to prevent ReDoS attacks.
    /// </summary>
    private const int MatchTimeoutMs = 1000;

    /// <summary>
    /// Matches health check endpoint: /health
    /// </summary>
    [GeneratedRegex("^/health$", RouteOptions, MatchTimeoutMs)]
    private static partial Regex HealthRegex();

    /// <summary>
    /// Matches package badge requests without organization: /badges/packages/{provider}/{package}
    /// Captures provider (nuget only) and package name.
    /// </summary>
    [GeneratedRegex("^/badges/packages/(?<provider>nuget)/(?<package>[^/]+)$", RouteOptions, MatchTimeoutMs)]
    private static partial Regex PackageBadgeRegex();

    /// <summary>
    /// Matches package badge requests with organization: /badges/packages/{provider}/{org}/{package}
    /// Captures provider (github only), organization, and package name.
    /// </summary>
    [GeneratedRegex("^/badges/packages/(?<provider>github)/(?<org>[^/]+)/(?<package>[^/]+)$", RouteOptions, MatchTimeoutMs)]
    private static partial Regex PackageBadgeWithOrgRegex();

    /// <summary>
    /// Matches test result badge requests: /badges/tests/{platform}/{owner}/{repo}/{branch}
    /// Captures platform (linux|windows|macos), repository owner, repository name, and branch.
    /// </summary>
    [GeneratedRegex("^/badges/tests/(?<platform>linux|windows|macos)/(?<owner>[^/]+)/(?<repo>[^/]+)/(?<branch>[^/]+)$", RouteOptions, MatchTimeoutMs)]
    private static partial Regex TestBadgeRegex();

    /// <summary>
    /// Matches test result ingestion endpoint: /tests/results
    /// Used for receiving test results from CI/CD pipelines.
    /// </summary>
    [GeneratedRegex("^/tests/results$", RouteOptions, MatchTimeoutMs)]
    private static partial Regex TestIngestionRegex();

    /// <summary>
    /// Matches test result redirect endpoint: /redirect/test-results/{platform}/{owner}/{repo}/{branch}
    /// Captures platform (linux|windows|macos), repository owner, repository name, and branch.
    /// Used for redirecting to external test result pages.
    /// </summary>
    [GeneratedRegex("^/redirect/test-results/(?<platform>linux|windows|macos)/(?<owner>[^/]+)/(?<repo>[^/]+)/(?<branch>[^/]+)$", RouteOptions, MatchTimeoutMs)]
    private static partial Regex TestRedirectRegex();

    /// <summary>
    /// Complete route definitions ordered from most specific to the least specific patterns.
    /// The order is critical for correct route resolution - more specific patterns must come first
    /// to prevent less specific patterns from matching incorrectly.
    /// </summary>
    public static readonly RouteEntry[] Routes =
    [
        // Health endpoint
        new("/health", HealthRegex(), typeof(IHealthCheckHandler), "GET", RequiresAuth: false),

        // Package badges (more specific route first)
        new("/badges/packages/{provider}/{org}/{package}", PackageBadgeWithOrgRegex(), typeof(IGithubPackagesBadgeHandler), "GET", RequiresAuth: false),
        new("/badges/packages/{provider}/{package}", PackageBadgeRegex(), typeof(INugetPackageBadgeHandler), "GET", RequiresAuth: false),

        // Test badges and ingestion
        new("/badges/tests/{platform}/{owner}/{repo}/{branch}", TestBadgeRegex(), typeof(ITestResultsBadgeHandler), "GET", RequiresAuth: false),
        new("/tests/results", TestIngestionRegex(), typeof(ITestResultIngestionHandler), "POST", RequiresAuth: true),

        // Test redirect
        new("/redirect/test-results/{platform}/{owner}/{repo}/{branch}", TestRedirectRegex(), typeof(ITestResultRedirectionHandler), "GET", RequiresAuth: false),
    ];
}
