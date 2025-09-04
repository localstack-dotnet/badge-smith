namespace BadgeSmith.Api.Domain.Services.Authentication.Contracts;

/// <summary>
/// Service for retrieving repository-specific HMAC secrets from AWS Secrets Manager.
/// Follows the same pattern as GitHubOrgSecretsService but for repo-level authentication.
/// </summary>
internal interface IRepoSecretsService
{
    /// <summary>
    /// Retrieves the HMAC secret for a specific repository.
    /// </summary>
    /// <param name="repoIdentifier">Repository identifier (e.g., "owner/repo")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the HMAC secret or failure information</returns>
    public Task<RepoSecretResult> GetRepoSecretAsync(string repoIdentifier, CancellationToken ct = default);
}
