using System.Text.Json.Serialization;

namespace BadgeSmith.Api.Features.HealthCheck;

internal record HealthCheckResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("time_stamp")] DateTimeOffset Timestamp);
