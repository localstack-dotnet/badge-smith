using BadgeSmith.Api.Core.Routing.Contracts;

namespace BadgeSmith.Api.Core.Routing.Patterns;

internal sealed class ExactPattern : IRoutePattern
{
    public string Literal { get; }

    public ExactPattern(string literal) => Literal = literal;

    public bool TryMatch(ReadOnlySpan<char> path, ref RouteValues values) =>
        path.Equals(Literal.AsSpan(), StringComparison.OrdinalIgnoreCase);
}
