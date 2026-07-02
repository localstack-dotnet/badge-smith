using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

#pragma warning disable MA0016
public sealed record LambdaHttpResponse(
    [property: JsonPropertyName("statusCode")] int StatusCode,
    [property: JsonPropertyName("headers")] Dictionary<string, string>? Headers,
    [property: JsonPropertyName("body")] string? Body);
#pragma warning restore MA0016

public sealed class LambdaRieClient(Uri invocationBase)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web);

    public async Task<LambdaHttpResponse> InvokeAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null,
        CancellationToken ct = default)
    {
        var evt = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["version"] = "2.0",
            ["routeKey"] = "$default",
            ["rawPath"] = path,
            ["headers"] = headers ?? new Dictionary<string, string>(StringComparer.Ordinal),
            ["requestContext"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["http"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["method"] = method, ["path"] = path },
                ["stage"] = "$default",
                ["requestId"] = Guid.NewGuid().ToString(),
            },
            ["body"] = body,
            ["isBase64Encoded"] = false,
        };

        using var content = new StringContent(JsonSerializer.Serialize(evt, Opts), Encoding.UTF8, "application/json");
        using var response = await Http.PostAsync(new Uri(invocationBase, "/2015-03-31/functions/function/invocations"), content, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<LambdaHttpResponse>(json, Opts)
               ?? throw new InvalidOperationException($"Unparseable RIE response: {json}");
    }
}
