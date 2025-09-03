using BadgeSmith.Api.Domain.Services.GitHub;

namespace BadgeSmith.Api.Domain.Services.Contracts;

internal interface IGitHubPackageServiceFactory
{
    public GitHubOrgSecretsService GitHubOrgSecretsService { get; }
    public GitHubPackageService GitHubPackageService { get; }
}
