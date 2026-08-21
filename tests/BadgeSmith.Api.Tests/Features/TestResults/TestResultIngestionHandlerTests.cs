using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Core.Security;
using BadgeSmith.Api.Core.Security.Contracts;
using BadgeSmith.Api.Features.TestResults.Contracts;
using BadgeSmith.Api.Features.TestResults.Handlers;
using BadgeSmith.Api.Tests.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Features.TestResults;

[Trait("Category", TestCategories.Unit)]
public sealed class TestResultIngestionHandlerTests
{
    private const string ValidTestPayloadJson =
        """{"platform":"linux","passed":3,"failed":0,"skipped":0,"total":3,"url_html":"https://example.com/run/42","timestamp":"2026-08-21T10:00:00Z","commit":"abc1234","run_id":"42","workflow_run_url":"https://example.com/workflow/42"}""";

    private const string ExpectedMissingOwnerBody =
        """{"message":"Owner parameter is required","error_details":[{"error_code":"MISSING_ROUTE_PARAMETER","property_name":"owner"}]}""";

    [Fact]
    public async Task HandleAsync_Should_Return_Safe_BadRequest_Body_When_Body_Is_Invalid_Json()
    {
        var sut = CreateHandler(out _, out _);

        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = "{invalid json payload",
        };

        var routeContext = new RouteContext(
            request,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["owner"] = "owner",
                ["repo"] = "repo",
                ["platform"] = "linux",
                ["branch"] = "main",
            });

        var response = await sut.HandleAsync(routeContext, TestContext.Current.CancellationToken);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Invalid JSON payload", response.Body);
    }

    [Fact]
    public async Task HandleAsync_Should_Map_Route_Parameters_To_Hmac_Context_Without_Swapping_Platform_And_Owner()
    {
        var hmacAuthenticationService = new Mock<IHmacAuthenticationService>();
        hmacAuthenticationService
            .Setup(service => service.ValidateRequestAsync(It.IsAny<HmacAuthContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HmacAuthenticationResult(new InvalidSignature("signature mismatch")));

        var sut = new TestResultIngestionHandler(
            Mock.Of<ILogger<TestResultIngestionHandler>>(),
            hmacAuthenticationService.Object,
            Mock.Of<ITestResultsService>());

        var routeContext = new RouteContext(
            new APIGatewayHttpApiV2ProxyRequest
            {
                Body = ValidTestPayloadJson,
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["x-signature"] = "signature",
                    ["x-timestamp"] = "2026-08-21T10:00:00Z",
                    ["x-nonce"] = "nonce-1",
                },
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["platform"] = "linux",
                ["owner"] = "acme-org",
                ["repo"] = "widget",
                ["branch"] = "main",
            });

        var response = await sut.HandleAsync(routeContext, TestContext.Current.CancellationToken);

        Assert.Equal(401, response.StatusCode);
        hmacAuthenticationService.Verify(
            service => service.ValidateRequestAsync(
                It.Is<HmacAuthContext>(context =>
                    context.Owner == "acme-org" &&
                    context.Repo == "widget" &&
                    context.Platform == "linux" &&
                    context.Branch == "main"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Standard_Error_Contract_When_Route_Parameter_Is_Missing()
    {
        var sut = CreateHandler(out _, out _);

        var routeContext = new RouteContext(
            new APIGatewayHttpApiV2ProxyRequest(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["platform"] = "linux",
                ["repo"] = "repo",
                ["branch"] = "main",
            });

        var response = await sut.HandleAsync(routeContext, TestContext.Current.CancellationToken);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(ExpectedMissingOwnerBody, response.Body);
        Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);
    }

    private static TestResultIngestionHandler CreateHandler(
        out Mock<IHmacAuthenticationService> hmacAuthenticationService,
        out Mock<ITestResultsService> testResultsService)
    {
        hmacAuthenticationService = new Mock<IHmacAuthenticationService>();
        testResultsService = new Mock<ITestResultsService>();

        return new TestResultIngestionHandler(
            Mock.Of<ILogger<TestResultIngestionHandler>>(),
            hmacAuthenticationService.Object,
            testResultsService.Object);
    }
}
