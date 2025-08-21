using System.Text.Json.Serialization;

namespace BadgeSmith.Domain.Models;

public record HealthCheckResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("time_stamp")] DateTimeOffset Timestamp);
