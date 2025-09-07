using System.Text.Json.Serialization;

namespace BadgeSmith.Api.Features.GitHub;

internal record GithubPackageVersion(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("package_html_url")] string PackageHtmlUrl,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTime UpdatedAt,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("metadata")] PackageMetadata Metadata
);

internal record PackageMetadata(
    [property: JsonPropertyName("package_type")] string PackageType
);
