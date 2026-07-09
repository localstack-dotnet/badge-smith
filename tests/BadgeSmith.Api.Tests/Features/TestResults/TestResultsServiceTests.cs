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
    [Fact]
    public async Task GetLatestTestResultAsync_Should_Query_Lowercase_GSI1PK_When_Route_Values_Have_Mixed_Case()
    {
        QueryRequest? captured = null;
        var dynamo = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        dynamo
            .Setup(d => d.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<QueryRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new QueryResponse { Items = [] });

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

        var payload = new TestResultPayload(
            Platform: "linux",
            Passed: 1,
            Failed: 0,
            Skipped: 0,
            Total: 1,
            UrlHtml: "https://example.com/html",
            Timestamp: DateTimeOffset.UtcNow,
            Commit: "abc123",
            RunId: "run-1",
            WorkflowRunUrl: "https://example.com/run");

        var request = new StoreTestResultRequest("owner", "repo", "linux", "main", payload);

        var result = await sut.StoreTestResultAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.Failure.IsT2); // Error
        var error = result.Failure.AsT2;
        Assert.Equal("Failed to store test result", error.Reason);
        Assert.DoesNotContain(secretLeak, error.Reason, StringComparison.Ordinal);
    }
}
