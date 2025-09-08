using System.Net;
using System.Security.Cryptography;

namespace BadgeSmith.Api.Core.Http;

internal sealed class ResilienceRetryHandler : DelegatingHandler
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;

    public ResilienceRetryHandler(HttpMessageHandler innerHandler, int maxRetries = 3, TimeSpan? baseDelay = null)
    {
        InnerHandler = innerHandler;
        _maxRetries = Math.Max(0, maxRetries);
        _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(150);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (!ShouldRetry(response.StatusCode) || attempt >= _maxRetries)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < _maxRetries)
            {
                // timeout: retry
            }
            catch (HttpRequestException) when (attempt < _maxRetries)
            {
                // transient network error: retry
            }

            var delay = ComputeBackoff(attempt);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        // If we reach here, it means we exhausted retries due to exceptions; do one last attempt to surface the exception.
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private TimeSpan ComputeBackoff(int attempt)
    {
        var pow = Math.Pow(2, attempt);
        var jitter = (GetSecureRandomDouble() * 0.5) + 0.5; // 0.5x-1.0x
        var ms = _baseDelay.TotalMilliseconds * pow * jitter;
        var capped = Math.Min(ms, 2000); // cap at 2s between retries
        return TimeSpan.FromMilliseconds(capped);
    }

    private static double GetSecureRandomDouble()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        var randomInt = BitConverter.ToUInt32(bytes);
        return randomInt / (double)uint.MaxValue;
    }
}
