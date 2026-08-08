using System.Security.Cryptography;
using System.Text;

namespace BadgeSmith.Protocol;

internal static class HmacCanonicalRequest
{
    private const string Scheme = "BADGESMITH-HMAC";
    private const string Method = "POST";

    public static string CreateCanonicalText(
        string platform,
        string owner,
        string repo,
        string branch,
        string timestamp,
        string nonce,
        string body)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(timestamp);
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(body);

        return string.Concat(
            Scheme,
            '\n',
            Method,
            '\n',
            CreateIngestionPath(platform, owner, repo, branch),
            '\n',
            timestamp.Trim(),
            '\n',
            nonce.Trim(),
            '\n',
            ComputeBodySha256Hex(body));
    }

    private static string CreateIngestionPath(string platform, string owner, string repo, string branch)
    {
        return string.Concat(
            "/tests/results/",
            Uri.EscapeDataString(platform.ToLowerInvariant()),
            '/',
            Uri.EscapeDataString(owner.ToLowerInvariant()),
            '/',
            Uri.EscapeDataString(repo.ToLowerInvariant()),
            '/',
            Uri.EscapeDataString(branch));
    }

    private static string ComputeBodySha256Hex(string body)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var hashBytes = SHA256.HashData(bodyBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
