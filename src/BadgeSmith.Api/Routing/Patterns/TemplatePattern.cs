using BadgeSmith.Api.Routing.Contracts;

namespace BadgeSmith.Api.Routing.Patterns;

internal sealed class TemplatePattern : IRoutePattern
{
    private readonly string[] _keys;
    private readonly string[] _literals;

    public TemplatePattern(string template)
    {
        // parse once at startup: split by '/', mark {param} vs literal
        // keep it simple; enforce fixed segment counts for speed
        var raw = template.AsSpan().TrimStart('/');
        var parts = raw.ToString().Split('/'); // one-time startup cost
        _keys = new string[parts.Length];
        _literals = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length >= 2 && part[0] == '{' && part[^1] == '}')
            {
                _keys[i] = part[1..^1];
                _literals[i] = string.Empty; // Mark as parameter segment
            }
            else
            {
                _literals[i] = part;
            }
        }
    }

    public bool TryMatch(ReadOnlySpan<char> path, ref RouteValues values)
    {
        var currentOffset = 0; // Track our position in the original path

        if (!path.IsEmpty && path[0] == '/')
        {
            path = path[1..];
            currentOffset = 1; // Skip the leading slash
        }

        for (var segIdx = 0; segIdx < _literals.Length; segIdx++)
        {
            var slash = path.IndexOf('/');
            var seg = slash < 0 ? path : path[..slash];

            if (_literals[segIdx].Length > 0)
            {
                if (!seg.Equals(_literals[segIdx].AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            else
            {
                // For parameters, we need the absolute position in the original path
                values.Set(_keys[segIdx], start: currentOffset, len: seg.Length);
            }

            // Move past this segment
            currentOffset += seg.Length;

            if (slash < 0)
            {
                // must be last segment
                return segIdx == _literals.Length - 1;
            }

            // Move past the slash
            currentOffset++;
            path = path[(slash + 1)..];
        }

        return path.Length == 0; // no extra segments
    }
}
