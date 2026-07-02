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
        var (rawPath, rawQueryString) = SplitPathAndQuery(path);
        var evt = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["version"] = "2.0",
            ["routeKey"] = "$default",
            ["rawPath"] = rawPath,
            ["rawQueryString"] = rawQueryString,
            ["queryStringParameters"] = ParseQueryString(rawQueryString),
            ["headers"] = headers ?? new Dictionary<string, string>(StringComparer.Ordinal),
            ["requestContext"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["http"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["method"] = method, ["path"] = rawPath },
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

    private static (string RawPath, string RawQueryString) SplitPathAndQuery(string path)
    {
        var queryStart = path.IndexOf('?', StringComparison.Ordinal);
        return queryStart < 0
            ? (path, string.Empty)
            : (path[..queryStart], path[(queryStart + 1)..]);
    }

    private static Dictionary<string, string> ParseQueryString(string rawQueryString)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(rawQueryString))
        {
            return parameters;
        }

        foreach (var pair in rawQueryString.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            var key = separator < 0 ? pair : pair[..separator];
            var value = separator < 0 ? string.Empty : pair[(separator + 1)..];
            parameters[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
        }

        return parameters;
    }
}
