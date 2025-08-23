using System.Runtime.InteropServices;

namespace BadgeSmith.Api.Routing;

[StructLayout(LayoutKind.Auto)]
internal ref struct RouteValues
{
    private readonly ReadOnlySpan<char> _path;
    private readonly Span<(string key, int start, int len)> _pairs;
    private int _count;

    public RouteValues(ReadOnlySpan<char> path, Span<(string, int, int)> buffer)
    {
        _path = path;
        _pairs = buffer; // Zero allocation - keep as Span!
        _count = 0;
    }

    public void Set(ReadOnlySpan<char> key, int start, int len)
        => _pairs[_count++] = (key.ToString(), start, len);

    public readonly bool TryGetSpan(string name, out ReadOnlySpan<char> value)
    {
        for (var i = 0; i < _count; i++)
        {
            if (!string.Equals(_pairs[i].key, name, StringComparison.Ordinal))
            {
                continue;
            }

            value = _path.Slice(_pairs[i].start, _pairs[i].len);
            return true;
        }

        value = default;
        return false;
    }

    public readonly bool TryGetString(string name, out string value)
    {
        if (TryGetSpan(name, out var s))
        {
            value = s.ToString();
            return true;
        }

        value = string.Empty;
        return false;
    }
}
