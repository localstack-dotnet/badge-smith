namespace BadgeSmith.Api.Core.Security.Contracts;

/// <summary>
/// Factory for creating authentication-related services.
/// Follows the same pattern as other service factories in the project.
/// </summary>
internal interface IAuthenticationServiceFactory
{
    public GitHubOrgSecretsService GitHubOrgSecretsService { get; }

    public INonceService NonceService { get; }

    public IHmacAuthenticationService HmacAuthenticationService { get; }
}
