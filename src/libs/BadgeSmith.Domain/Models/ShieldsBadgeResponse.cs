using System.Text.Json.Serialization;

namespace BadgeSmith.Domain.Models;

public record ShieldsBadgeResponse(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("color")] string? Color = null,
    [property: JsonPropertyName("labelColor")] string? LabelColor = null,
    [property: JsonPropertyName("isError")] bool? IsError = null,
    [property: JsonPropertyName("namedLogo")] string? NamedLogo = null,
    [property: JsonPropertyName("logoSvg")] string? LogoSvg = null,
    [property: JsonPropertyName("logoColor")] string? LogoColor = null,
    [property: JsonPropertyName("logoSize")] string? LogoSize = null,
    [property: JsonPropertyName("style")] string? Style = null
);
