using System.Text.RegularExpressions;

namespace BadgeSmith.Api.Routing;

/// <summary>
/// Immutable route definition containing all metadata required for HTTP request routing and handler resolution.
/// Combines human-readable templates with high-performance compiled regex patterns for efficient route matching.
/// </summary>
/// <param name="Template">Human-readable route template with parameter placeholders for documentation and debugging (e.g., "/badges/packages/{provider}/{package}").</param>
/// <param name="CompiledRegex">Source-generated regex pattern optimized for high performance route matching with named capture groups for parameter extraction.</param>
/// <param name="Handler">Handler class type that implements IRouteHandler interface to process matched requests.</param>
/// <param name="Method">HTTP method verb (GET, POST, PUT, DELETE, etc.) that this route accepts. Case-insensitive matching is performed.</param>
/// <param name="RequiresAuth">Flag indicating whether this route requires authentication validation before request processing.</param>
/// <param name="Match">Optional regex match result populated during route resolution, containing captured groups and route parameters. Null until the route is resolved.</param>
internal record RouteEntry(string Template, Regex CompiledRegex, Type Handler, string Method, bool RequiresAuth, Match? Match = null);
