using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BadgeSmith.Api.Core.Security;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

public static class HmacTestSigner
{
    public static (string Signature, string Timestamp, string Nonce) Sign(
        string owner,
        string repo,
        string platform,
        string branch,
        string body,
        string secret,
        DateTimeOffset? timestamp = null,
        string? nonce = null)
    {
        var ts = (timestamp ?? DateTimeOffset.UtcNow).UtcDateTime
            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var requestNonce = nonce ?? Guid.NewGuid().ToString("N");
        var canonicalText = HmacCanonicalRequest.CreateCanonicalText(platform, owner, repo, branch, ts, requestNonce, body);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(canonicalText));
        return ($"sha256={Convert.ToHexString(hash).ToLowerInvariant()}", ts, requestNonce);
    }
}
