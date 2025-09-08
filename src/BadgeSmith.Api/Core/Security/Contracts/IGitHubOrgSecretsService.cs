namespace BadgeSmith.Api.Core.Security.Contracts;

/// <summary>
/// Service for managing GitHub authentication secrets with caching.
/// Retrieves Personal Access Tokens from AWS Secrets Manager and caches them in-memory.
/// </summary>
internal interface IGitHubOrgSecretsService
{
    public Task<GithubSecretResult> GetGitHubTokenAsync(string orgName, string tokenType, CancellationToken ct = default);
}
