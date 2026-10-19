using BadgeSmith.Api.Core.Routing.Contracts;

namespace BadgeSmith.Api.Core.Routing.Patterns;

internal sealed class TemplatePattern : IRoutePattern
{
    private readonly string[] _keys;
    private readonly string[] _literals;

    public TemplatePattern(string template)
    {
        var raw = template.AsSpan().TrimStart('/');
        var segmentCount = 1;
        foreach (var character in raw)
        {
            if (character == '/')
            {
                segmentCount++;
            }
        }

        _keys = new string[segmentCount];
        _literals = new string[segmentCount];
        var i = 0;
        foreach (var range in raw.Split('/'))
        {
            var part = raw[range];
            if (part.Length >= 2 && part[0] == '{' && part[^1] == '}')
            {
                _keys[i] = part[1..^1].ToString();
                _literals[i] = string.Empty;
            }
            else
            {
                _literals[i] = part.ToString();
            }

            i++;
        }
    }

    public bool TryMatch(ReadOnlySpan<char> path, ref RouteValues values)
    {
        var currentOffset = 0;

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
                // must be the last segment
                return segIdx == _literals.Length - 1;
            }

            // Move past the slash
            currentOffset++;
            path = path[(slash + 1)..];
        }

        return path.Length == 0; // no extra segments
    }
}
