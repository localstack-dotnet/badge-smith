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
}
