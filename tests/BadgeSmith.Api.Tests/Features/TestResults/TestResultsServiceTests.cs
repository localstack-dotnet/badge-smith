using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BadgeSmith.Api.Features.TestResults;
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
}
