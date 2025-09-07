using System.Text.Json.Serialization;

namespace BadgeSmith.Api.Features.TestResults;

internal record TestResultIngestionResponse(
    [property: JsonPropertyName("test_result_id")] string TestResultId,
    [property: JsonPropertyName("repository")] string Repository,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);
