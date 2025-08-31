using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace BadgeSmith.Api.Infrastructure.Routing;

[StructLayout(LayoutKind.Auto)]
internal ref struct RouteValues
{
    private readonly ReadOnlySpan<char> _path;
    private readonly Span<(string key, int start, int len)> _pairs;
    private int _count;

    public RouteValues(ReadOnlySpan<char> path, Span<(string, int, int)> buffer)
    {
        _path = path;
        _pairs = buffer;
        _count = 0;
    }

    public void Set(string key, int start, int len)
    {
        if (_count >= _pairs.Length)
        {
            throw new InvalidOperationException("RouteValues buffer is full.");
        }

        _pairs[_count++] = (key, start, len);
    }

    public readonly bool TryGetSpan(string name, out ReadOnlySpan<char> value)
    {
        for (var i = 0; i < _count; i++)
        {
            if (!string.Equals(_pairs[i].key, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = _path.Slice(_pairs[i].start, _pairs[i].len);
            return true;
        }

        value = default;
        return false;
    }

    public readonly bool TryGetString(string name, out string? value)
    {
        if (TryGetSpan(name, out var s))
        {
            value = System.Web.HttpUtility.UrlDecode(s.ToString());
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Gets a route parameter value as a URL-decoded string.
    /// This is the preferred method for extracting route parameters as it handles URL encoding automatically.
    /// </summary>
    /// <param name="name">The parameter name (case-insensitive)</param>
    /// <returns>The URL-decoded parameter value, or null if the parameter doesn't exist</returns>
    public readonly string? GetString(string name)
    {
        return TryGetString(name, out var value) ? value : null;
    }

    public readonly IReadOnlyDictionary<string, string> ToImmutableDictionary()
    {
        var b = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < _count; i++)
        {
            var (key, start, len) = _pairs[i];
            var rawValue = _path.Slice(start, len).ToString();
            b[key] = System.Web.HttpUtility.UrlDecode(rawValue); // URL decode and overwrite if duplicate
        }

        return b.ToImmutable();
    }
}
