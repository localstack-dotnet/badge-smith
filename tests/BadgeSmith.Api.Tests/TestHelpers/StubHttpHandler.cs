using System.Net;
using System.Text;

namespace BadgeSmith.Api.Tests.TestHelpers;

internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _scriptedResponses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<CancellationToken> ObservedTokens { get; } = [];

    public void Respond(HttpStatusCode statusCode, string content, Action<HttpResponseMessage>? configure = null)
    {
        _scriptedResponses.Enqueue((_, _) => Task.FromResult(CreateResponse(statusCode, content, configure)));
    }

    /// <summary>
    /// Records the observed cancellation token and holds the request open until that token fires,
    /// which surfaces <see cref="OperationCanceledException"/> through <c>HttpClient</c>.
    /// </summary>
    public void HoldUntilCancelled()
    {
        _scriptedResponses.Enqueue(async (_, cancellationToken) =>
        {
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using (cancellationToken.Register(() => held.SetResult()))
            {
                await held.Task.ConfigureAwait(false);
            }

            await Task.FromCanceled(cancellationToken).ConfigureAwait(false);

            return CreateResponse(HttpStatusCode.OK, string.Empty, configure: null);
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        ObservedTokens.Add(cancellationToken);

        return await _scriptedResponses.Dequeue()(request, cancellationToken).ConfigureAwait(false);
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string content, Action<HttpResponseMessage>? configure)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };

        configure?.Invoke(response);
        return response;
    }
}
