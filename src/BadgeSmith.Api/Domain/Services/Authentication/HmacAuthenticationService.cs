using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BadgeSmith.Api.Domain.Services.Authentication.Contracts;
using Microsoft.Extensions.Logging;

namespace BadgeSmith.Api.Domain.Services.Authentication;

/// <summary>
/// HMAC-SHA256 authentication service with replay protection.
/// Validates request signatures using repository-specific secrets and prevent replay attacks via nonces.
/// </summary>
internal sealed class HmacAuthenticationService : IHmacAuthenticationService
{
    private readonly IGitHubOrgSecretsService _gitHubOrgSecretsService;
    private readonly INonceService _nonceService;
    private readonly ILogger<HmacAuthenticationService> _logger;

    private static readonly TimeSpan MaxTimestampAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxTimestampSkew = TimeSpan.FromMinutes(1);
    private const string TokenType = "TestData";

    public HmacAuthenticationService(IGitHubOrgSecretsService gitHubOrgSecretsService, INonceService nonceService, ILogger<HmacAuthenticationService> logger)
    {
        _gitHubOrgSecretsService = gitHubOrgSecretsService ?? throw new ArgumentNullException(nameof(gitHubOrgSecretsService));
        _nonceService = nonceService ?? throw new ArgumentNullException(nameof(nonceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HmacAuthenticationResult> ValidateRequestAsync(HmacAuthContext authContext, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(HmacAuthenticationService)}.{nameof(ValidateRequestAsync)}");

        ValidateHmacAuthContext(authContext);

        if (!TryParseTimestamp(authContext.Timestamp, out var requestTimestamp, out var timestampError))
        {
            return timestampError;
        }

        var repoIdentifier = $"{authContext.Owner}/{authContext.Repo}/{authContext.Repo}/{authContext.Branch}";
        var nonceResult = await _nonceService.ValidateAndMarkNonceAsync(authContext.Nonce, repoIdentifier, requestTimestamp, ct).ConfigureAwait(false);

        if (!nonceResult.IsSuccess)
        {
            return nonceResult.Failure.Match<HmacAuthenticationResult>
            (
                alreadyUsed => alreadyUsed,
                error => error
            );
        }

        var secretResult = await _gitHubOrgSecretsService.GetGitHubTokenAsync(authContext.Owner, TokenType, ct).ConfigureAwait(false);
        if (secretResult is { IsSuccess: false, GithubSecret: null })
        {
            return secretResult.Failure.Match<HmacAuthenticationResult>
            (
                notFound => new RepoSecretNotFound(notFound.Reason),
                error => error
            );
        }

        var secret = secretResult.GithubSecret!;

        if (!ValidateHmacSignature(authContext.Signature, authContext.RequestBody, secret))
        {
            _logger.LogWarning("Invalid HMAC signature for repository {RepoIdentifier}", repoIdentifier);
            return new InvalidSignature("HMAC signature verification failed");
        }

        _logger.LogInformation("Successfully authenticated request for repository {RepoIdentifier}", repoIdentifier);
        return new AuthenticatedRequest(repoIdentifier, requestTimestamp);
    }

    private static void ValidateHmacAuthContext(HmacAuthContext routeContext)
    {
        ArgumentNullException.ThrowIfNull(routeContext);
        ArgumentNullException.ThrowIfNull(routeContext.Owner);
        ArgumentNullException.ThrowIfNull(routeContext.Repo);
        ArgumentNullException.ThrowIfNull(routeContext.Platform);
        ArgumentNullException.ThrowIfNull(routeContext.Branch);
        ArgumentNullException.ThrowIfNull(routeContext.Signature);
        ArgumentNullException.ThrowIfNull(routeContext.Timestamp);
        ArgumentNullException.ThrowIfNull(routeContext.Nonce);
        ArgumentNullException.ThrowIfNull(routeContext.RequestBody);
    }

    private static bool TryParseTimestamp(string timestampStr, out DateTimeOffset requestTimestamp, out InvalidTimestamp? error)
    {
        error = null;

        if (!DateTimeOffset.TryParse(timestampStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out requestTimestamp))
        {
            error = new InvalidTimestamp($"Invalid timestamp format: {timestampStr}. Expected ISO 8601 format.");
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var age = now - requestTimestamp;
        var skew = requestTimestamp - now;

        if (age > MaxTimestampAge)
        {
            error = new InvalidTimestamp($"Request timestamp is too old. Age: {age.TotalMinutes:F1} minutes, max allowed: {MaxTimestampAge.TotalMinutes} minutes.");
            return false;
        }

        if (skew > MaxTimestampSkew)
        {
            error = new InvalidTimestamp(
                $"Request timestamp is too far in the future. Skew: {skew.TotalMinutes:F1} minutes, max allowed: {MaxTimestampSkew.TotalMinutes} minutes.");
            return false;
        }

        return true;
    }

    private static bool ValidateHmacSignature(string providedSignature, string payload, string secret)
    {
        if (!providedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedHash = providedSignature[7..];

        var computedHash = ComputeHmacSha256(payload, secret);

        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(providedHash), Convert.FromHexString(computedHash));
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
