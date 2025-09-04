namespace BadgeSmith.Api.Domain.Services.Authentication.Contracts;

/// <summary>
/// Factory for creating authentication-related services.
/// Follows the same pattern as other service factories in the project.
/// </summary>
internal interface IAuthenticationServiceFactory
{
    public IRepoSecretsService RepoSecretsService { get; }
    public INonceService NonceService { get; }
    public IHmacAuthenticationService HmacAuthenticationService { get; }
}
