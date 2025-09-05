namespace BadgeSmith.Api.Domain.Services.Authentication.Contracts;

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
    /// <param name="orgName">The GitHub organization name.</param>
    /// <param name="tokenType">The token type</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>
    /// The Personal Access Token if found; otherwise, null.
    /// </returns>
    public Task<GithubSecretResult> GetGitHubTokenAsync(string orgName, string tokenType, CancellationToken ct = default);
}
