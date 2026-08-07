using System.Security.Cryptography;
using System.Text;
using BadgeSmith.Api.Core.Security;

namespace BadgeSmith.Tools.Infrastructure;

internal static class HmacSigner
{
    public static string CreateSignature(
        string owner,
        string repo,
        string platform,
        string branch,
        string timestamp,
        string nonce,
        string payload,
        string secret)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(timestamp);
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(secret);

        var canonicalText = HmacCanonicalRequest.CreateCanonicalText(platform, owner, repo, branch, timestamp, nonce, payload);
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(canonicalText);
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        return "sha256=" + Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
