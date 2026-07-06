using System.Text.Json.Serialization;

namespace BadgeSmith.Tools.Infrastructure;

internal sealed record TestResultPayload(
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("passed")] int Passed,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("skipped")] int Skipped,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("url_html")] string UrlHtml,
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("commit")] string Commit,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("workflow_run_url")] string WorkflowRunUrl);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(TestResultPayload))]
internal sealed partial class ToolJsonSerializerContext : JsonSerializerContext;
