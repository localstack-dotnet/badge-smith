using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using BadgeSmith.Api.Core;
using BadgeSmith.Api.Core.Routing.Contracts;
using BadgeSmith.Api.Tests.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Http;

[Trait("Category", TestCategories.Unit)]
public sealed class LambdaRequestProcessorTests
{
    [Fact]
    public async Task HandleAsync_Should_Return_Router_Response_Unchanged_When_Route_Succeeds()
    {
        var expected = new APIGatewayHttpApiV2ProxyResponse { StatusCode = 200, Body = "router body" };
        var processor = CreateProcessor(router => router
            .Setup(service => service.RouteAsync(It.IsAny<APIGatewayHttpApiV2ProxyRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected));

        var response = await processor.HandleAsync(CreateRequest(), new TestLambdaContext());

        Assert.Same(expected, response);
    }

    [Fact]
    public async Task HandleAsync_Should_Pass_Request_Unchanged_And_Cancellable_Token_To_Router()
    {
        var request = CreateRequest();
        CancellationToken observedToken = default;
        APIGatewayHttpApiV2ProxyRequest? observedRequest = null;

        var processor = CreateProcessor(router => router
            .Setup(service => service.RouteAsync(It.IsAny<APIGatewayHttpApiV2ProxyRequest>(), It.IsAny<CancellationToken>()))
            .Callback<APIGatewayHttpApiV2ProxyRequest, CancellationToken>((req, token) =>
            {
                observedRequest = req;
                observedToken = token;
            })
            .ReturnsAsync(new APIGatewayHttpApiV2ProxyResponse { StatusCode = 200 }));

        _ = await processor.HandleAsync(request, new TestLambdaContext());

        Assert.Same(request, observedRequest);
        Assert.True(observedToken.CanBeCanceled);
        Assert.False(observedToken.IsCancellationRequested);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Safe_500_Body_When_Router_Throws()
    {
        var processor = CreateProcessor(router => router
            .Setup(service => service.RouteAsync(It.IsAny<APIGatewayHttpApiV2ProxyRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom")));

        var response = await processor.HandleAsync(CreateRequest(), new TestLambdaContext());

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("An error occurred processing the request", response.Body);
    }

    [Fact]
    public async Task HandleAsync_Should_Open_Request_Id_Logging_Scope_For_Each_Request()
    {
        var scopeStates = new List<object?>();
        var logger = new CapturingLogger(scopeStates.Add);
        var processor = new LambdaRequestProcessor(StubRouter(new APIGatewayHttpApiV2ProxyResponse { StatusCode = 200 }), logger);

        var context = new TestLambdaContext { AwsRequestId = "request-42" };
        _ = await processor.HandleAsync(CreateRequest(), context);

        Assert.Equal(["request-42"], scopeStates);
    }

    [Fact]
    public async Task HandleAsync_Should_Emit_Handling_Metadata_When_Route_Succeeds()
    {
        var logger = new RecordingLogger();
        var expected = new APIGatewayHttpApiV2ProxyResponse { StatusCode = 200, Body = "router body" };
        var processor = new LambdaRequestProcessor(StubRouter(expected), logger);

        var response = await processor.HandleAsync(CreateRequest(), new TestLambdaContext { AwsRequestId = "metadata-request" });

        Assert.Same(expected, response);
        var record = Assert.Single(logger.Records);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(1001, record.EventId.Id);
        Assert.Equal("LambdaRequestHandling", record.EventId.Name);
        Assert.Equal("Handling GET /health", record.Message);
        Assert.Equal([
            new KeyValuePair<string, object?>("Method", "GET"),
            new KeyValuePair<string, object?>("Path", "/health"),
            new KeyValuePair<string, object?>("{OriginalFormat}", "Handling {Method} {Path}"),
        ], record.State);
        Assert.Null(record.Exception);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task HandleAsync_Should_Respect_Level_Enablement_When_Router_Throws(bool informationEnabled, bool errorEnabled)
    {
        var logger = TestBase.SetupILogger(isEnabled: false);
        logger.Setup(service => service.IsEnabled(LogLevel.Information)).Returns(informationEnabled);
        logger.Setup(service => service.IsEnabled(LogLevel.Error)).Returns(errorEnabled);
        var scopeLogger = new RecordingLogger();
        logger.Setup(service => service.BeginScope(It.IsAny<string>())).Returns<string>(scopeLogger.BeginScope);
        var failure = new InvalidOperationException("private-upstream-detail");
        var request = CreateRequest();
        var routed = false;
        var router = new Mock<IApiRouter>();
        router.Setup(service => service.RouteAsync(It.IsAny<APIGatewayHttpApiV2ProxyRequest>(), It.IsAny<CancellationToken>()))
            .Callback<APIGatewayHttpApiV2ProxyRequest, CancellationToken>((observed, token) =>
            {
                Assert.Same(request, observed);
                Assert.True(token.CanBeCanceled);
                Assert.False(token.IsCancellationRequested);
                Assert.Equal("level-request", Assert.IsType<string>(scopeLogger.CurrentScope?.State));
                Assert.False(scopeLogger.CurrentScope.IsDisposed);
                routed = true;
            })
            .ThrowsAsync(failure);
        var processor = new LambdaRequestProcessor(router.Object, logger.Object);

        var response = await processor.HandleAsync(request, new TestLambdaContext { AwsRequestId = "level-request" });

        Assert.True(routed);
        Assert.Equal(500, response.StatusCode);
        Assert.Equal("An error occurred processing the request", response.Body);
        var logs = logger.Invocations.Where(call => call.Method.Name == nameof(ILogger.Log)).ToArray();
        Assert.Equal(informationEnabled ? 1 : 0, logs.Count(call => Assert.IsType<LogLevel>(call.Arguments[0]) == LogLevel.Information));
        Assert.Equal(errorEnabled ? 1 : 0, logs.Count(call => Assert.IsType<LogLevel>(call.Arguments[0]) == LogLevel.Error));
        Assert.Equal((informationEnabled ? 1 : 0) + (errorEnabled ? 1 : 0), logs.Length);
        var scope = Assert.Single(logger.Invocations, call => call.Method.Name == nameof(ILogger.BeginScope));
        Assert.Equal("level-request", Assert.IsType<string>(Assert.Single(scope.Arguments)));
        Assert.Null(scopeLogger.CurrentScope);
        Assert.Equal(1, Assert.Single(scopeLogger.Scopes).DisposeCount);
    }

    [Theory]
    [InlineData(null, null, "UNKNOWN", "/", "Handling UNKNOWN /")]
    [InlineData(null, "/raw", "UNKNOWN", "/raw", "Handling UNKNOWN /raw")]
    [InlineData("POST", null, "POST", "/", "Handling POST /")]
    [InlineData("", "", "", "", "Handling  ")]
    [InlineData(" \t", "\t ", " \t", "\t ", "Handling  \t \t ")]
    public async Task HandleAsync_Should_Preserve_Method_And_Path_Values_When_Logging_Request(
        string? method, string? path, string expectedMethod, string expectedPath, string expectedMessage)
    {
        var logger = new RecordingLogger();
        var request = CreateRequest();
        request.RequestContext.Http.Method = method;
        request.RequestContext.Http.Path = path;
        var processor = new LambdaRequestProcessor(StubRouter(new APIGatewayHttpApiV2ProxyResponse { StatusCode = 200 }), logger);

        _ = await processor.HandleAsync(request, new TestLambdaContext());

        var record = Assert.Single(logger.Records);
        Assert.Equal([
            new KeyValuePair<string, object?>("Method", expectedMethod),
            new KeyValuePair<string, object?>("Path", expectedPath),
            new KeyValuePair<string, object?>("{OriginalFormat}", "Handling {Method} {Path}"),
        ], record.State);
        Assert.Equal(expectedMessage, record.Message);
    }

    [Fact]
    public async Task HandleAsync_Should_Keep_Scope_Active_Across_Await_When_Route_Succeeds()
    {
        var logger = new RecordingLogger();
        var completion = new TaskCompletionSource<APIGatewayHttpApiV2ProxyResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new APIGatewayHttpApiV2ProxyResponse { StatusCode = 202, Body = "awaited response" };
        var router = new Mock<IApiRouter>();
        router.Setup(service => service.RouteAsync(It.IsAny<APIGatewayHttpApiV2ProxyRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => logger.Timeline.Add("router"))
            .Returns(completion.Task);
        var processor = new LambdaRequestProcessor(router.Object, logger);

        var pending = processor.HandleAsync(CreateRequest(), new TestLambdaContext { AwsRequestId = "await-success" });
        Assert.False(pending.IsCompleted);
        var scope = Assert.Single(logger.Scopes);
        Assert.Same(scope, logger.CurrentScope);
        Assert.Equal("await-success", Assert.IsType<string>(scope.State));
        Assert.False(scope.IsDisposed);
        Assert.Equal(["scope", "Information", "router"], logger.Timeline);
        completion.SetResult(expected);
        var response = await pending;

        Assert.Same(expected, response);
        Assert.Same(scope, Assert.Single(logger.Records).Scope);
        Assert.Equal(1, scope.DisposeCount);
        Assert.Null(logger.CurrentScope);
        Assert.Equal(["scope", "Information", "router", "dispose"], logger.Timeline);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_Should_Log_Original_Error_In_Active_Scope_When_Route_Fails_After_Await(bool cancellation)
    {
        var logger = new RecordingLogger();
        var completion = new TaskCompletionSource<APIGatewayHttpApiV2ProxyResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        Exception failure = cancellation
            ? new OperationCanceledException("private-cancellation-detail", cancelled.Token)
            : new InvalidOperationException("private-upstream-detail");
        var request = CreateRequest();
        var router = new Mock<IApiRouter>();
        router.Setup(service => service.RouteAsync(It.IsAny<APIGatewayHttpApiV2ProxyRequest>(), It.IsAny<CancellationToken>()))
            .Callback<APIGatewayHttpApiV2ProxyRequest, CancellationToken>((observed, token) =>
            {
                Assert.Same(request, observed);
                Assert.True(token.CanBeCanceled);
                Assert.False(token.IsCancellationRequested);
                logger.Timeline.Add("router");
            })
            .Returns(completion.Task);
        var processor = new LambdaRequestProcessor(router.Object, logger);

        var pending = processor.HandleAsync(request, new TestLambdaContext { AwsRequestId = "await-failure" });
        Assert.False(pending.IsCompleted);
        var scope = Assert.Single(logger.Scopes);
        Assert.Same(scope, logger.CurrentScope);
        Assert.Equal("await-failure", Assert.IsType<string>(scope.State));
        Assert.False(scope.IsDisposed);
        Assert.Equal(["scope", "Information", "router"], logger.Timeline);
        completion.SetException(failure);
        var response = await pending;

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("An error occurred processing the request", response.Body);
        Assert.DoesNotContain(failure.Message, response.Body, StringComparison.Ordinal);
        Assert.Collection(logger.Records,
            handling =>
            {
                Assert.Equal(LogLevel.Information, handling.Level);
                Assert.Equal(1001, handling.EventId.Id);
                Assert.Equal("LambdaRequestHandling", handling.EventId.Name);
                Assert.Null(handling.Exception);
                Assert.Same(scope, handling.Scope);
            },
            error =>
            {
                Assert.Equal(LogLevel.Error, error.Level);
                Assert.Equal(1002, error.EventId.Id);
                Assert.Equal("LambdaUnhandledError", error.EventId.Name);
                Assert.Equal("Unhandled error", error.Message);
                Assert.Equal([new KeyValuePair<string, object?>("{OriginalFormat}", "Unhandled error")], error.State);
                Assert.Same(failure, error.Exception);
                Assert.Same(scope, error.Scope);
            });
        Assert.Equal(1, scope.DisposeCount);
        Assert.Null(logger.CurrentScope);
        Assert.Equal(["scope", "Information", "router", "Error", "dispose"], logger.Timeline);
    }

    [Fact]
    public async Task HandleAsync_Should_Isolate_Request_Scopes_When_Requests_Are_Consecutive()
    {
        var logger = new RecordingLogger();
        var router = new Mock<IApiRouter>();
        router.SetupSequence(service => service.RouteAsync(It.IsAny<APIGatewayHttpApiV2ProxyRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new APIGatewayHttpApiV2ProxyResponse { StatusCode = 200 })
            .ThrowsAsync(new InvalidOperationException("second-request-error"));
        var processor = new LambdaRequestProcessor(router.Object, logger);

        _ = await processor.HandleAsync(CreateRequest(), new TestLambdaContext { AwsRequestId = "first-request" });
        Assert.Null(logger.CurrentScope);
        Assert.True(Assert.Single(logger.Scopes).IsDisposed);
        _ = await processor.HandleAsync(CreateRequest(), new TestLambdaContext { AwsRequestId = "second-request" });

        Assert.Collection(logger.Scopes,
            first => Assert.Equal("first-request", Assert.IsType<string>(first.State)),
            second => Assert.Equal("second-request", Assert.IsType<string>(second.State)));
        Assert.NotSame(logger.Scopes[0], logger.Scopes[1]);
        Assert.All(logger.Scopes, scope => Assert.Equal(1, scope.DisposeCount));
        Assert.Collection(logger.Records,
            first => Assert.Same(logger.Scopes[0], first.Scope),
            second => Assert.Same(logger.Scopes[1], second.Scope),
            error => Assert.Same(logger.Scopes[1], error.Scope));
        Assert.Null(logger.CurrentScope);
    }

    private static LambdaRequestProcessor CreateProcessor(Action<Mock<IApiRouter>> configure)
    {
        var router = new Mock<IApiRouter>();
        configure(router);
        return new LambdaRequestProcessor(router.Object, NullLogger.Instance);
    }

    private static IApiRouter StubRouter(APIGatewayHttpApiV2ProxyResponse response)
    {
        var router = new Mock<IApiRouter>();
        _ = router
            .Setup(service => service.RouteAsync(It.IsAny<APIGatewayHttpApiV2ProxyRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        return router.Object;
    }

    private static APIGatewayHttpApiV2ProxyRequest CreateRequest() => new()
    {
        RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
        {
            Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = "GET", Path = "/health" },
        },
    };

    private sealed class CapturingLogger(Action<object?> onBeginScope) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            onBeginScope(state);
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed record LogRecord(LogLevel Level, EventId EventId, IReadOnlyList<KeyValuePair<string, object?>> State,
        Exception? Exception, string Message, RequestScope? Scope);

    private sealed class RequestScope(object state, Action onDispose) : IDisposable
    {
        public object State { get; } = state;
        public int DisposeCount { get; private set; }
        public bool IsDisposed => DisposeCount != 0;

        public void Dispose()
        {
            DisposeCount++;
            onDispose();
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<LogRecord> Records { get; } = [];
        public List<string> Timeline { get; } = [];
        public List<RequestScope> Scopes { get; } = [];
        public RequestScope? CurrentScope { get; private set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            Timeline.Add("scope");
            CurrentScope = new RequestScope(state, () =>
            {
                Timeline.Add("dispose");
                CurrentScope = null;
            });
            Scopes.Add(CurrentScope);
            return CurrentScope;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var fields = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(state);
            Timeline.Add(logLevel.ToString());
            Assert.NotNull(CurrentScope);
            Assert.False(CurrentScope.IsDisposed);
            Records.Add(new LogRecord(logLevel, eventId, fields.ToArray(), exception, formatter(state, exception), CurrentScope));
        }
    }
}
