using System.Net;
using System.Net.Http.Headers;
using BadgeSmith.Api.Core.Http;
using Xunit;

namespace BadgeSmith.Api.Tests.TestHelpers;

internal static class UpstreamFetchMatrix
{
    public const string FirstRequestHasNoValidatorsAndWritesCache = nameof(FirstRequestHasNoValidatorsAndWritesCache);
    public const string CachedWeakEtagIsReplayedVerbatim = nameof(CachedWeakEtagIsReplayedVerbatim);
    public const string CachedLastModifiedProducesIfModifiedSince = nameof(CachedLastModifiedProducesIfModifiedSince);
    public const string NotModifiedWithCacheReusesPayload = nameof(NotModifiedWithCacheReusesPayload);
    public const string NotModifiedWithCacheWritesMergedEntryWithTtl = nameof(NotModifiedWithCacheWritesMergedEntryWithTtl);
    public const string NotModifiedWithoutCacheReturnsErrorResult = nameof(NotModifiedWithoutCacheReturnsErrorResult);
    public const string ResponseEtagOverridesCachedEtag = nameof(ResponseEtagOverridesCachedEtag);
    public const string LastModifiedPrecedenceIsContentThenDateThenCached = nameof(LastModifiedPrecedenceIsContentThenDateThenCached);
    public const string ErrorStatusIsNotCachedAndBodyIsNotInterpreted = nameof(ErrorStatusIsNotCachedAndBodyIsNotInterpreted);
    public const string CancellationIsForwardedToSendAsync = nameof(CancellationIsForwardedToSendAsync);
    public const string UpstreamValidatorsNeverBecomeResultIdentity = nameof(UpstreamValidatorsNeverBecomeResultIdentity);

    public static TheoryData<string> ScenarioNames =>
    [
        FirstRequestHasNoValidatorsAndWritesCache,
        CachedWeakEtagIsReplayedVerbatim,
        CachedLastModifiedProducesIfModifiedSince,
        NotModifiedWithCacheReusesPayload,
        NotModifiedWithCacheWritesMergedEntryWithTtl,
        NotModifiedWithoutCacheReturnsErrorResult,
        ResponseEtagOverridesCachedEtag,
        LastModifiedPrecedenceIsContentThenDateThenCached,
        ErrorStatusIsNotCachedAndBodyIsNotInterpreted,
        CancellationIsForwardedToSendAsync,
        UpstreamValidatorsNeverBecomeResultIdentity,
    ];

    public sealed record Fixture(string ExpectedCacheKey, string OkPayload, string ExpectedVersion)
    {
        public static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    }

    public sealed record Outcome(bool IsSuccess, string? VersionString, DateTimeOffset? LastModifiedUtc, string? FailureReason);

    public static async Task RunAsync(
        string scenario,
        StubHttpHandler handler,
        RecordingAppCache cache,
        Fixture fixture,
        Func<CancellationToken, Task<Outcome>> act)
    {
        switch (scenario)
        {
            case FirstRequestHasNoValidatorsAndWritesCache:
                await FirstRequestHasNoValidators(handler, cache, fixture, act);
                break;
            case CachedWeakEtagIsReplayedVerbatim:
                await CachedWeakEtagReplay(handler, cache, fixture, act);
                break;
            case CachedLastModifiedProducesIfModifiedSince:
                await CachedLastModifiedReplay(handler, cache, fixture, act);
                break;
            case NotModifiedWithCacheReusesPayload:
                await NotModifiedReusesPayload(handler, cache, fixture, act);
                break;
            case NotModifiedWithCacheWritesMergedEntryWithTtl:
                await NotModifiedMergedWriteRefreshesTtl(handler, cache, fixture, act);
                break;
            case NotModifiedWithoutCacheReturnsErrorResult:
                await NotModifiedWithoutCacheReturnsError(handler, cache, act);
                break;
            case ResponseEtagOverridesCachedEtag:
                await ResponseEtagOverride(handler, cache, fixture, act);
                break;
            case LastModifiedPrecedenceIsContentThenDateThenCached:
                await LastModifiedPrecedence(handler, cache, fixture, act);
                break;
            case ErrorStatusIsNotCachedAndBodyIsNotInterpreted:
                await ErrorStatusSkipsCacheAndBody(handler, cache, act);
                break;
            case CancellationIsForwardedToSendAsync:
                await CancellationForwarded(handler, act);
                break;
            case UpstreamValidatorsNeverBecomeResultIdentity:
                await ValidatorsStayOutOfResultIdentity(handler, fixture, act);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown upstream matrix scenario.");
        }
    }

    private static async Task FirstRequestHasNoValidators(
        StubHttpHandler handler,
        RecordingAppCache cache,
        Fixture fixture,
        Func<CancellationToken, Task<Outcome>> act)
    {
        handler.Respond(HttpStatusCode.OK, fixture.OkPayload);

        var outcome = await act(TestContext.Current.CancellationToken);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(fixture.ExpectedVersion, outcome.VersionString);

        var request = Assert.Single(handler.Requests);
        Assert.Empty(request.Headers.IfNoneMatch);
        Assert.Null(request.Headers.IfModifiedSince);

        var set = Assert.Single(cache.Sets);
        Assert.Equal(fixture.ExpectedCacheKey, set.Key);
        Assert.Equal(Fixture.CacheTtl, set.Ttl);

        var entry = Assert.IsType<UpstreamCacheEntry>(set.Entry);
        Assert.Equal(fixture.OkPayload, entry.Payload);
    }

    private static async Task CachedWeakEtagReplay(
        StubHttpHandler handler,
        RecordingAppCache cache,
        Fixture fixture,
        Func<CancellationToken, Task<Outcome>> act)
    {
        cache.Seed(fixture.ExpectedCacheKey, new UpstreamCacheEntry(fixture.OkPayload, "W/\"weak-1\"", null));
        handler.Respond(HttpStatusCode.OK, fixture.OkPayload);

        _ = await act(TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        var validator = Assert.Single(request.Headers.IfNoneMatch);
        Assert.True(validator.IsWeak);
        Assert.Equal("W/\"weak-1\"", validator.ToString());
    }

    private static async Task CachedLastModifiedReplay(
        StubHttpHandler handler,
        RecordingAppCache cache,
        Fixture fixture,
        Func<CancellationToken, Task<Outcome>> act)
    {
        var lastModified = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        cache.Seed(fixture.ExpectedCacheKey, new UpstreamCacheEntry(fixture.OkPayload, null, lastModified));
        handler.Respond(HttpStatusCode.OK, fixture.OkPayload);

        _ = await act(TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(lastModified, request.Headers.IfModifiedSince);
    }

    private static async Task NotModifiedReusesPayload(
        StubHttpHandler handler,
        RecordingAppCache cache,
        Fixture fixture,
        Func<CancellationToken, Task<Outcome>> act)
    {
        cache.Seed(fixture.ExpectedCacheKey, new UpstreamCacheEntry(fixture.OkPayload, "\"cached-1\"", null));
        handler.Respond(HttpStatusCode.NotModified, string.Empty);

        var outcome = await act(TestContext.Current.CancellationToken);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(fixture.ExpectedVersion, outcome.VersionString);
    }

    private static async Task NotModifiedMergedWriteRefreshesTtl(
        StubHttpHandler handler,
        RecordingAppCache cache,
        Fixture fixture,
        Func<CancellationToken, Task<Outcome>> act)
    {
        var responseDate = new DateTimeOffset(2026, 8, 20, 9, 30, 0, TimeSpan.Zero);
        cache.Seed(fixture.ExpectedCacheKey, new UpstreamCacheEntry(fixture.OkPayload, "\"old\"", null));
        handler.Respond(
            HttpStatusCode.NotModified,
            string.Empty,
            response =>
            {
                response.Headers.ETag = new EntityTagHeaderValue("\"merged\"", isWeak: true);
                response.Headers.Date = responseDate;
            });

        var outcome = await act(TestContext.Current.CancellationToken);

        Assert.True(outcome.IsSuccess);
        var set = Assert.Single(cache.Sets);
        Assert.Equal(Fixture.CacheTtl, set.Ttl);

        var entry = Assert.IsType<UpstreamCacheEntry>(set.Entry);
        Assert.Equal(fixture.OkPayload, entry.Payload);
        Assert.Equal("W/\"merged\"", entry.ETag);
        Assert.Equal(responseDate, entry.LastModified);
    }

    private static async Task NotModifiedWithoutCacheReturnsError(
        StubHttpHandler handler,
        RecordingAppCache cache,
        Func<CancellationToken, Task<Outcome>> act)
    {
        handler.Respond(HttpStatusCode.NotModified, string.Empty);

        var outcome = await act(TestContext.Current.CancellationToken);

        Assert.False(outcome.IsSuccess);
        Assert.Equal("Received 304 Not Modified without a cached entry", outcome.FailureReason);
        Assert.Empty(cache.Sets);
    }

    private static async Task ResponseEtagOverride(
        StubHttpHandler handler,
        RecordingAppCache cache,
        Fixture fixture,
        Func<CancellationToken, Task<Outcome>> act)
    {
        cache.Seed(fixture.ExpectedCacheKey, new UpstreamCacheEntry(fixture.OkPayload, "\"old\"", null));
        handler.Respond(
            HttpStatusCode.NotModified,
            string.Empty,
            response => response.Headers.ETag = new EntityTagHeaderValue("\"new\""));

        _ = await act(TestContext.Current.CancellationToken);

        var set = Assert.Single(cache.Sets);
        var entry = Assert.IsType<UpstreamCacheEntry>(set.Entry);
        Assert.Equal("\"new\"", entry.ETag);
        Assert.Equal(fixture.OkPayload, entry.Payload);
    }

    private static async Task LastModifiedPrecedence(
        StubHttpHandler handler,
        RecordingAppCache cache,
        Fixture fixture,
        Func<CancellationToken, Task<Outcome>> act)
    {
        var cachedStamp = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var contentStamp = new DateTimeOffset(2026, 8, 10, 8, 0, 0, TimeSpan.Zero);
        var dateStamp = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);
        var seededEntry = new UpstreamCacheEntry(fixture.OkPayload, null, cachedStamp);

        cache.Seed(fixture.ExpectedCacheKey, seededEntry);
        handler.Respond(
            HttpStatusCode.NotModified,
            string.Empty,
            response => response.Content.Headers.LastModified = contentStamp);
        _ = await act(TestContext.Current.CancellationToken);
        var contentHeaderEntry = ConsumeSingleSet(cache);

        ResetForSubScenario(handler, cache, fixture, seededEntry);
        handler.Respond(
            HttpStatusCode.NotModified,
            string.Empty,
            response => response.Headers.Date = dateStamp);
        _ = await act(TestContext.Current.CancellationToken);
        var dateHeaderEntry = ConsumeSingleSet(cache);

        ResetForSubScenario(handler, cache, fixture, seededEntry);
        handler.Respond(HttpStatusCode.NotModified, string.Empty);
        _ = await act(TestContext.Current.CancellationToken);
        var cachedFallbackEntry = ConsumeSingleSet(cache);

        Assert.Equal(contentStamp, contentHeaderEntry.LastModified);
        Assert.Equal(dateStamp, dateHeaderEntry.LastModified);
        Assert.Equal(cachedStamp, cachedFallbackEntry.LastModified);
    }

    private static async Task ErrorStatusSkipsCacheAndBody(
        StubHttpHandler handler,
        RecordingAppCache cache,
        Func<CancellationToken, Task<Outcome>> act)
    {
        handler.Respond(HttpStatusCode.InternalServerError, "<not-json-at-all>");

        var outcome = await act(TestContext.Current.CancellationToken);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("API error", outcome.FailureReason, StringComparison.Ordinal);
        Assert.Empty(cache.Sets);
    }

    private static async Task CancellationForwarded(
        StubHttpHandler handler,
        Func<CancellationToken, Task<Outcome>> act)
    {
        using var cts = new CancellationTokenSource();
        handler.HoldUntilCancelled();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
            {
                var call = act(cts.Token);
                await cts.CancelAsync();
                await call;
            });
        Assert.True(Assert.Single(handler.ObservedTokens).IsCancellationRequested);
    }

    private static async Task ValidatorsStayOutOfResultIdentity(
        StubHttpHandler handler,
        Fixture fixture,
        Func<CancellationToken, Task<Outcome>> act)
    {
        var contentStamp = new DateTimeOffset(2026, 8, 12, 6, 45, 0, TimeSpan.Zero);
        handler.Respond(
            HttpStatusCode.OK,
            fixture.OkPayload,
            response =>
            {
                response.Headers.ETag = new EntityTagHeaderValue("\"upstream-identity\"");
                response.Content.Headers.LastModified = contentStamp;
            });

        var outcome = await act(TestContext.Current.CancellationToken);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(fixture.ExpectedVersion, outcome.VersionString);
        Assert.Equal(contentStamp, outcome.LastModifiedUtc);
    }

    private static UpstreamCacheEntry ConsumeSingleSet(RecordingAppCache cache)
    {
        var set = Assert.Single(cache.Sets);
        return Assert.IsType<UpstreamCacheEntry>(set.Entry);
    }

    private static void ResetForSubScenario(StubHttpHandler handler, RecordingAppCache cache, Fixture fixture, UpstreamCacheEntry seededEntry)
    {
        handler.Requests.Clear();
        cache.Sets.Clear();
        cache.Seed(fixture.ExpectedCacheKey, seededEntry);
    }
}
