using System.Text.Json.Serialization;

namespace BadgeSmith.Api.Domain.Models;

internal record ErrorResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("error_details")] IEnumerable<ErrorDetail>? ErrorDetails = null);

internal record ErrorDetail(
    [property: JsonPropertyName("error_code")] string ErrorCode,
    [property: JsonPropertyName("property_name")] string PropertyName);
