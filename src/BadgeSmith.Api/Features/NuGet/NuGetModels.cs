using System.Text.Json.Serialization;

namespace BadgeSmith.Api.Features.NuGet;

internal record NuGetIndexResponse([property: JsonPropertyName("versions")] string[] Versions);
