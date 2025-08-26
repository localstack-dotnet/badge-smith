using System.Collections.Immutable;

namespace BadgeSmith.Api.Tests.TestHelpers;

/// <summary>
/// Realistic test data for badge routing tests using real-world package names and URLs.
/// </summary>
public static class TestData
{
    /// <summary>
    /// Real NuGet packages for testing.
    /// </summary>
    public static class NuGetPackages
    {
        public const string NewtonsoftJson = "Newtonsoft.Json";
        public const string MicrosoftExtensionsHttp = "Microsoft.Extensions.Http";
        public const string AutoMapper = "AutoMapper";
        public const string FluentValidation = "FluentValidation";
        public const string LocalStackClient = "LocalStack.Client";
        public const string EntityFrameworkCore = "Microsoft.EntityFrameworkCore";
    }

    /// <summary>
    /// Real GitHub packages for testing.
    /// </summary>
    public static class GitHubPackages
    {
        public static readonly (string Org, string Package)[] Packages =
        [
            ("localstack-dotnet", "localstack.client"),
            ("microsoft", "vscode"),
            ("facebook", "react"),
            ("dotnet", "aspnetcore"),
            ("AutoMapper", "AutoMapper"),
            ("JamesNK", "Newtonsoft.Json"),
        ];
    }

    /// <summary>
    /// Real repository information for test result badges.
    /// </summary>
    public static class TestRepositories
    {
        public static readonly (string Platform, string Owner, string Repo, string Branch)[] Repositories =
        [
            ("linux", "localstack-dotnet", "dotnet-aspire-for-localstack", "main"),
            ("windows", "microsoft", "vscode", "main"),
            ("macos", "facebook", "react", "main"),
            ("linux", "dotnet", "aspnetcore", "release/8.0"),
            ("windows", "AutoMapper", "AutoMapper", "main"),
            ("linux", "localstack-dotnet", "localstack.client", "feature/awesome-badge"),
        ];
    }

    /// <summary>
    /// Common route patterns used in the application.
    /// </summary>
    public static class RoutePatterns
    {
        public const string Health = "/health";
        public const string NugetPackage = "/badges/packages/{provider}/{package}";
        public const string GitHubPackage = "/badges/packages/{provider}/{org}/{package}";
        public const string TestBadge = "/badges/tests/{platform}/{owner}/{repo}/{branch}";
        public const string TestIngestion = "/tests/results";
        public const string TestRedirect = "/redirect/test-results/{platform}/{owner}/{repo}/{branch}";
    }

    /// <summary>
    /// Real URLs that should match various patterns.
    /// </summary>
    public static class RealUrls
    {
        /// <summary>
        /// NuGet package URLs
        /// </summary>
        public static readonly string[] NuGetUrls =
        [
            "/badges/packages/nuget/Newtonsoft.Json",
            "/badges/packages/nuget/Microsoft.Extensions.Http",
            "/badges/packages/nuget/AutoMapper",
            "/badges/packages/nuget/FluentValidation",
            "/badges/packages/nuget/LocalStack.Client",
        ];

        /// <summary>
        /// GitHub package URLs
        /// </summary>
        public static readonly string[] GitHubUrls =
        [
            "/badges/packages/github/localstack-dotnet/localstack.client",
            "/badges/packages/github/microsoft/vscode",
            "/badges/packages/github/facebook/react",
            "/badges/packages/github/dotnet/aspnetcore",
            "/badges/packages/github/AutoMapper/AutoMapper",
        ];

        /// <summary>
        /// Test badge URLs (with URL-encoded branches)
        /// </summary>
        public static readonly string[] TestUrls =
        [
            "/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main",
            "/badges/tests/windows/microsoft/vscode/main",
            "/badges/tests/macos/facebook/react/main",
            "/badges/tests/linux/dotnet/aspnetcore/release%2F8.0",
            "/badges/tests/linux/localstack-dotnet/localstack.client/feature%2Fawesome-badge",
        ];

        /// <summary>
        /// Redirect URLs
        /// </summary>
        public static readonly string[] RedirectUrls =
        [
            "/redirect/test-results/linux/localstack-dotnet/dotnet-aspire-for-localstack/main",
            "/redirect/test-results/windows/microsoft/vscode/main",
            "/redirect/test-results/linux/localstack-dotnet/localstack.client/feature%2Fawesome-badge",
        ];

        /// <summary>
        /// URLs that should NOT match any pattern
        /// </summary>
        public static readonly string[] InvalidUrls =
        [
            "/badges",
            "/badges/packages",
            "/badges/packages/nuget",
            "/badges/packages/invalid/too/many/segments/here",
            "/badges/tests",
            "/badges/tests/linux",
            "/badges/tests/linux/owner",
            "/invalid/path",
            "/health/extra",
            "",
        ];
    }

    /// <summary>
    /// HTTP methods for testing.
    /// </summary>
    public static class HttpMethods
    {
        public const string Get = "GET";
        public const string Post = "POST";
        public const string Put = "PUT";
        public const string Delete = "DELETE";
        public const string Head = "HEAD";
        public const string Options = "OPTIONS";
        public const string Patch = "PATCH";
    }

    /// <summary>
    /// Expected parameter extractions for various URLs.
    /// </summary>
    public static readonly ImmutableDictionary<string, ImmutableDictionary<string, string>> ExpectedParameters =
        new Dictionary<string, ImmutableDictionary<string, string>>
            (StringComparer.OrdinalIgnoreCase)
        {
            ["/badges/packages/nuget/Newtonsoft.Json"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "nuget",
                ["package"] = "Newtonsoft.Json",
            }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),

            ["/badges/packages/github/localstack-dotnet/localstack.client"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "github",
                ["org"] = "localstack-dotnet",
                ["package"] = "localstack.client",
            }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),

            ["/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/main"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["platform"] = "linux",
                ["owner"] = "localstack-dotnet",
                ["repo"] = "dotnet-aspire-for-localstack",
                ["branch"] = "main",
            }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),

            ["/badges/tests/linux/localstack-dotnet/localstack.client/feature%2Fawesome-badge"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["platform"] = "linux",
                ["owner"] = "localstack-dotnet",
                ["repo"] = "localstack.client",
                ["branch"] = "feature%2Fawesome-badge",
            }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
        }.ToImmutableDictionary();
}
