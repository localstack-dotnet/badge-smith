using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

public static class HmacTestSigner
{
    public static (string Signature, string Timestamp, string Nonce) Sign(
        string body, string secret, DateTimeOffset? timestamp = null, string? nonce = null)
    {
        var ts = (timestamp ?? DateTimeOffset.UtcNow).UtcDateTime
            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        return ($"sha256={Convert.ToHexString(hash).ToLowerInvariant()}", ts, nonce ?? Guid.NewGuid().ToString("N"));
    }
}
