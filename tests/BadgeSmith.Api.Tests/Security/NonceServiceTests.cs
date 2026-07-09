using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BadgeSmith.Api.Core.Caching;
using BadgeSmith.Api.Core.Security;
using BadgeSmith.Api.Tests.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Security;

[Trait("Category", TestCategories.Unit)]
public sealed class NonceServiceTests
{
    [Fact]
    public async Task ValidateAndMarkNonceAsync_Should_Return_Error_Without_Exception_Message_When_DynamoDb_Throws()
    {
        const string secretLeak = "INTERNAL-DYNAMODB-CONNECTION-DETAILS";

        var dynamo = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        dynamo
            .Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(secretLeak));

        var cache = new Mock<IAppCache>(MockBehavior.Strict);
        cache
            .Setup(c => c.TryGetValue(It.IsAny<string>(), out It.Ref<bool>.IsAny))
            .Returns(false);

        var sut = new NonceService(
            dynamo.Object,
            cache.Object,
            Mock.Of<ILogger<NonceService>>(),
            nonceTableName: "badge-smith-nonce");

        var result = await sut.ValidateAndMarkNonceAsync(
            "nonce-abc",
            "owner/repo/platform/branch",
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.Failure.IsT1); // Error
        var error = result.Failure.AsT1;
        Assert.Equal("Failed to validate nonce", error.Reason);
        Assert.DoesNotContain(secretLeak, error.Reason, StringComparison.Ordinal);
    }
}
