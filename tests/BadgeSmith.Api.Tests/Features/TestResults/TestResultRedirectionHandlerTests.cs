using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Features.TestResults.Contracts;
using BadgeSmith.Api.Features.TestResults.Handlers;
using BadgeSmith.Api.Features.TestResults.Models;
using BadgeSmith.Api.Tests.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Features.TestResults;

[Trait("Category", TestCategories.Unit)]
public sealed class TestResultRedirectionHandlerTests
{
    private const string ExpectedMissingOwnerBody =
        """{"message":"Owner parameter is required","error_details":[{"error_code":"MISSING_ROUTE_PARAMETER","property_name":"owner"}]}""";

    private const string ResultUrl = "https://example.com/run/42";

    [Fact]
    public async Task HandleAsync_Should_Pass_Parameters_In_Service_Owner_Repo_Platform_Branch_Order_When_Redirecting()
    {
        var testResultsService = new Mock<ITestResultsService>();
        testResultsService
            .Setup(service => service.GetLatestTestResultAsync("acme-org", "widget", "linux", "main", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEntity());

        var sut = new TestResultRedirectionHandler(Mock.Of<ILogger<TestResultRedirectionHandler>>(), testResultsService.Object);

        var response = await sut.HandleAsync(CreateRouteContext(), TestContext.Current.CancellationToken);

        Assert.Equal(302, response.StatusCode);
        Assert.Equal(ResultUrl, response.Headers["Location"]);
        testResultsService.Verify(
            service => service.GetLatestTestResultAsync("acme-org", "widget", "linux", "main", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Standard_Error_Contract_When_Route_Parameter_Is_Missing()
    {
        var sut = new TestResultRedirectionHandler(
            Mock.Of<ILogger<TestResultRedirectionHandler>>(),
            Mock.Of<ITestResultsService>());

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

    private static RouteContext CreateRouteContext() => new(
        new APIGatewayHttpApiV2ProxyRequest(),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["platform"] = "linux",
            ["owner"] = "acme-org",
            ["repo"] = "widget",
            ["branch"] = "main",
        });

    private static TestResultEntity CreateEntity() => new(
        Pk: "TEST#acme-org#widget",
        Sk: "RESULT#linux#main#2026-08-21T10:00:00.000Z",
        Gsi1Pk: "LATEST#acme-org#widget#linux#main",
        Gsi1Sk: "2026-08-21T10:00:00.000Z",
        Owner: "acme-org",
        Repo: "widget",
        Platform: "linux",
        Branch: "main",
        Passed: 3,
        Failed: 0,
        Skipped: 0,
        Total: 3,
        Timestamp: DateTimeOffset.UnixEpoch,
        Commit: "abc1234",
        RunId: "42",
        UrlHtml: ResultUrl,
        WorkflowRunUrl: "https://example.com/workflow/42",
        CreatedAt: DateTimeOffset.UnixEpoch,
        Ttl: 0);
}
