using BadgeSmith.Api.Domain.Services.Github;

namespace BadgeSmith.Api.Domain.Services.Contracts;

internal interface IGithubPackageServiceFactory
{
    public GithubOrgSecretsService GithubOrgSecretsService { get; }
}
