using System.Net.Http.Headers;
using System.Text;

namespace BadgeSmith.Api.Tests.Testing.Infrastructure;

public sealed record ContractHttpResponse(
    int StatusCode,
    IDictionary<string, string> Headers,
    string? Body);

public sealed class ContractHttpClient(Uri baseAddress)
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(60),
    };

    public async Task<ContractHttpResponse> InvokeAsync(
        string method,
        string path,
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(baseAddress, path));

        if (body is not null)
        {
            var contentType = GetContentType(headers);
            request.Content = new StringContent(body, Encoding.UTF8, contentType);
        }

        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                if (string.Equals(key, "content-type", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(key, "if-none-match", StringComparison.OrdinalIgnoreCase))
                {
                    // Use the strongly-typed API to ensure the ETag value is parsed
                    // and validated. Plain TryAddWithoutValidation may produce malformed
                    // values for structured headers.
                    request.Headers.IfNoneMatch.ParseAdd(value);
                    continue;
                }

                _ = request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddHeaders(responseHeaders, response.Headers);
        AddHeaders(responseHeaders, response.Content.Headers);

        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return new ContractHttpResponse((int)response.StatusCode, responseHeaders, responseBody.Length == 0 ? null : responseBody);
    }

    private static string GetContentType(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is not null && headers.TryGetValue("content-type", out var contentType) && !string.IsNullOrWhiteSpace(contentType))
        {
            return contentType;
        }

        return "application/json";
    }

    private static void AddHeaders(Dictionary<string, string> target, HttpHeaders source)
    {
        foreach (var header in source)
        {
            var value = string.Join(',', header.Value);
            target[header.Key] = value;
            target[header.Key.ToLowerInvariant()] = value;
        }
    }
}
