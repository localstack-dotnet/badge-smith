using System.Text.Json.Serialization;

namespace BadgeSmith.Domain.Models;

public record ErrorResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("error_details")] IEnumerable<ErrorDetail>? ErrorDetails = null);

public record ErrorDetail(
    [property: JsonPropertyName("error_code")] string ErrorCode,
    [property: JsonPropertyName("property_name")] string PropertyName);
