using System.Text;

namespace BadgeSmith.Tools.Infrastructure;

internal sealed class BadgeSmithUrlBuilder
{
    private readonly string _baseUrl;

    private BadgeSmithUrlBuilder(Uri baseUri)
    {
        _baseUrl = baseUri.AbsoluteUri.TrimEnd('/');
    }

    public static bool TryCreate(string? value, out BadgeSmithUrlBuilder builder, out string error)
    {
        builder = null!;
        error = "";

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Base URL is required.";
            return false;
        }

        var trimmedValue = value.Trim();
        if (!Uri.TryCreate(trimmedValue, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(baseUri.Host))
        {
            error = "Base URL must be an absolute HTTP or HTTPS URL.";
            return false;
        }

        if (!string.IsNullOrEmpty(baseUri.UserInfo))
        {
            error = "Base URL must not contain credentials.";
            return false;
        }

        if (trimmedValue.Contains('?', StringComparison.Ordinal))
        {
            error = "Base URL must not contain a query string.";
            return false;
        }

        if (trimmedValue.Contains('#', StringComparison.Ordinal))
        {
            error = "Base URL must not contain a fragment.";
            return false;
        }

        builder = new BadgeSmithUrlBuilder(baseUri);
        return true;
    }

    public static BadgeSmithUrlBuilder Create(string value)
    {
        return TryCreate(value, out var builder, out var error)
            ? builder
            : throw new ArgumentException(error, nameof(value));
    }

    public string BuildIngestUrl(string platform, string owner, string repository, string branch)
    {
        return BuildUrl("tests", "results", platform, owner, repository, branch);
    }

    public string BuildBadgeUrl(string platform, string owner, string repository, string branch)
    {
        return BuildUrl("badges", "tests", platform, owner, repository, branch);
    }

    public string BuildRedirectUrl(string platform, string owner, string repository, string branch)
    {
        return BuildUrl("redirect", "test-results", platform, owner, repository, branch);
    }

    private string BuildUrl(params string[] segments)
    {
        var url = new StringBuilder(_baseUrl);
        foreach (var segment in segments)
        {
            url.Append('/').Append(Uri.EscapeDataString(segment));
        }

        return url.ToString();
    }
}
