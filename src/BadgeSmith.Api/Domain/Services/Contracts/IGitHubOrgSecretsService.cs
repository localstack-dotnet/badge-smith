using BadgeSmith.Api.Domain.Services.GitHub;

namespace BadgeSmith.Api.Domain.Services.Contracts;

/// <summary>
/// Service for managing GitHub authentication secrets with caching.
/// Retrieves Personal Access Tokens from AWS Secrets Manager and caches them in-memory.
/// </summary>
internal interface IGitHubOrgSecretsService
{
    /// <summary>
    /// Retrieves a GitHub Personal Access Token for the specified organization.
    /// Uses in-memory caching with TTL to minimize Secrets Manager calls.
    /// </summary>
    /// <param name="organizationName">The GitHub organization name.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>
    /// The Personal Access Token if found; otherwise, null.
    /// </returns>
    public Task<GithubSecretResult> GetGitHubTokenAsync(string organizationName, CancellationToken ct = default);
}
