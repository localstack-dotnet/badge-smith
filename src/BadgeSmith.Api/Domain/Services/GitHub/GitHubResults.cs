using System.Text.Json.Serialization;
using BadgeSmith.Api.Domain.Services.Package;
using OneOf;

namespace BadgeSmith.Api.Domain.Services.GitHub;

internal record SecretNotFound(string Reason) : NotFoundFailure(Reason);

internal sealed record PackageNotFound(string Reason) : NotFoundFailure(Reason);

internal sealed record UnauthorizedPackageAccess(string Reason) : Error(Reason);

internal sealed record ForbiddenPackageAccess(string Reason) : Error(Reason);

[GenerateOneOf]
internal partial class GithubSecretResult : OneOfBase<string, SecretNotFound, Error>
{
    public bool IsSuccess => IsT0 && AsT0 != null;

    public bool IsFailure => !IsSuccess;

    public string? GithubSecret => IsT0 ? AsT0 : null;

    public OneOf<SecretNotFound, Error> Failure
    {
        get
        {
            if (TryPickT1(out var notFound, out _))
            {
                return notFound;
            }

            if (TryPickT2(out var error, out _))
            {
                return error;
            }

            throw new InvalidOperationException("Failure was not found");
        }
    }
}

[GenerateOneOf]
internal sealed partial class GitHubPackageResult : OneOfBase<GitHubPackageInfo, PackageNotFound, InvalidVersionRange, UnauthorizedPackageAccess, ForbiddenPackageAccess, Error>
{
    public bool IsSuccess => IsT0 && AsT0 != null;
    public GitHubPackageInfo? GitHubPackageInfo => IsT0 ? AsT0 : null;

    public OneOf<PackageNotFound, InvalidVersionRange, UnauthorizedPackageAccess, ForbiddenPackageAccess, Error> Failure => IsT0
        ? throw new InvalidOperationException("Result is successful")
        : Match<OneOf<PackageNotFound, InvalidVersionRange, UnauthorizedPackageAccess, ForbiddenPackageAccess, Error>>(
            _ => throw new InvalidOperationException("Result is successful"),
            notFound => notFound,
            range => range,
            unAuth => unAuth,
            forbidden => forbidden,
            error => error
        );
}

internal sealed record GitHubPackageInfo(string PackageName, string Organization, string VersionString, bool IsPrerelease, DateTimeOffset? LastModifiedUtc = null);

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
