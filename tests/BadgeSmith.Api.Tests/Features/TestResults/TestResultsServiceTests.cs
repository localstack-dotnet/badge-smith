using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BadgeSmith.Api.Features.TestResults;
using BadgeSmith.Api.Features.TestResults.Models;
using BadgeSmith.Api.Tests.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Features.TestResults;

[Trait("Category", TestCategories.Unit)]
public sealed class TestResultsServiceTests
{
    [Theory]
    [InlineData("http://example.com/tests")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:password@example.com/tests")]
    public async Task StoreTestResultAsync_Should_Reject_Payload_When_Result_Url_Is_Insecure(string urlHtml)
    {
        var dynamo = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        var sut = new TestResultsService(
            dynamo.Object,
            tableName: "badge-smith-test-result",
            Mock.Of<ILogger<TestResultsService>>());
        var payload = CreatePayload(urlHtml);

        var result = await sut.StoreTestResultAsync(
            new StoreTestResultRequest("owner", "repo", "linux", "main", payload),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.Failure.IsT0);
        dynamo.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("http://example.com/run")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:password@example.com/run")]
    public async Task StoreTestResultAsync_Should_Reject_Payload_When_Workflow_Run_Url_Is_Insecure(string workflowRunUrl)
    {
        var dynamo = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        var sut = new TestResultsService(
            dynamo.Object,
            tableName: "badge-smith-test-result",
            Mock.Of<ILogger<TestResultsService>>());
        var payload = CreatePayload("https://example.com/tests", workflowRunUrl);

        var result = await sut.StoreTestResultAsync(
            new StoreTestResultRequest("owner", "repo", "linux", "main", payload),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.Failure.IsT0);
        dynamo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StoreTestResultAsync_Should_Accept_Payload_When_Https_Result_Origin_Differs_From_Workflow_Run()
    {
        var dynamo = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        dynamo
            .Setup(client => client.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());
        var sut = new TestResultsService(
            dynamo.Object,
            tableName: "badge-smith-test-result",
            Mock.Of<ILogger<TestResultsService>>());
        var payload = CreatePayload(
            "https://reports.example.com/tests",
            "https://github.example.com/owner/repo/actions/runs/42");

        var result = await sut.StoreTestResultAsync(
            new StoreTestResultRequest("owner", "repo", "linux", "main", payload),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        dynamo.Verify(
            client => client.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLatestTestResultAsync_Should_Query_Lowercase_GSI1PK_When_Route_Values_Have_Mixed_Case()
    {
        QueryRequest? captured = null;
        var dynamo = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        dynamo
            .Setup(d => d.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<QueryRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new QueryResponse
            {
                Items = []
            });

        var sut = new TestResultsService(
            dynamo.Object,
            tableName: "badge-smith-test-result",
            Mock.Of<ILogger<TestResultsService>>());

        _ = await sut.GetLatestTestResultAsync("LocalStack-DotNet", "Badge-Smith", "Linux", "Master", TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("LATEST#localstack-dotnet#badge-smith#linux#master", captured!.ExpressionAttributeValues[":gsi1pk"].S);
    }

    [Fact]
    public async Task StoreTestResultAsync_Should_Return_Error_Without_Exception_Message_When_DynamoDb_Throws()
    {
        const string secretLeak = "INTERNAL-DYNAMODB-THROTTLE-DETAILS";

        var dynamo = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        dynamo
            .Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(secretLeak));

        var sut = new TestResultsService(
            dynamo.Object,
            tableName: "badge-smith-test-result",
            Mock.Of<ILogger<TestResultsService>>());

        var payload = CreatePayload("https://example.com/html");

        var request = new StoreTestResultRequest("owner", "repo", "linux", "main", payload);

        var result = await sut.StoreTestResultAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.Failure.IsT2); // Error
        var error = result.Failure.AsT2;
        Assert.Equal("Failed to store test result", error.Reason);
        Assert.DoesNotContain(secretLeak, error.Reason, StringComparison.Ordinal);
    }

    private static TestResultPayload CreatePayload(
        string urlHtml,
        string workflowRunUrl = "https://example.com/run")
    {
        return new TestResultPayload(
            Platform: "linux",
            Passed: 1,
            Failed: 0,
            Skipped: 0,
            Total: 1,
            UrlHtml: urlHtml,
            Timestamp: DateTimeOffset.UtcNow,
            Commit: "abc123",
            RunId: "run-1",
            WorkflowRunUrl: workflowRunUrl);
    }
}
