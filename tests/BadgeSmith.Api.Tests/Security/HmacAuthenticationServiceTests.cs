using BadgeSmith.Api.Core.Security;
using BadgeSmith.Api.Core.Security.Contracts;
using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Api.Tests.Testing.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Security;

[Trait("Category", TestCategories.Unit)]
public sealed class HmacAuthenticationServiceTests
{
    [Fact]
    public async Task ValidateRequestAsync_Should_Use_Platform_In_Repository_Identifier_When_Signature_Is_Valid()
    {
        const string owner = "localstack-dotnet";
        const string repo = "badge-smith";
        const string platform = "windows";
        const string branch = "feature/iteration0";
        const string secret = "test-secret";
        const string body = "{\"total\":1}";

        var (signature, timestamp, nonce) = HmacTestSigner.Sign(body, secret);
        string? nonceRepoIdentifier = null;

        var secretsService = new Mock<IGitHubOrgSecretsService>(MockBehavior.Strict);
        secretsService
            .Setup(service => service.GetGitHubTokenAsync(owner, "TestData", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GithubSecretResult)secret);

        var nonceService = new Mock<INonceService>(MockBehavior.Strict);
        nonceService
            .Setup(service => service.ValidateAndMarkNonceAsync(nonce, It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateTimeOffset, CancellationToken>((_, repoIdentifier, _, _) => nonceRepoIdentifier = repoIdentifier)
            .ReturnsAsync((NonceValidationResult)new ValidNonce(nonce, DateTimeOffset.UtcNow));

        var service = new HmacAuthenticationService(
            secretsService.Object,
            nonceService.Object,
            Mock.Of<ILogger<HmacAuthenticationService>>());

        var result = await service.ValidateRequestAsync(
            new HmacAuthContext(owner, repo, platform, branch, signature, timestamp, nonce, body),
            TestContext.Current.CancellationToken);

        var expectedRepoIdentifier = $"{owner}/{repo}/{platform}/{branch}";
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedRepoIdentifier, nonceRepoIdentifier);
        Assert.Equal(expectedRepoIdentifier, result.AuthenticatedRequest?.RepoIdentifier);

        secretsService.VerifyAll();
        nonceService.VerifyAll();
    }

    [Fact]
    public async Task ValidateRequestAsync_Should_Not_Mark_Nonce_When_Signature_Is_Invalid()
    {
        const string owner = "localstack-dotnet";
        const string repo = "badge-smith";
        const string platform = "windows";
        const string branch = "feature/iteration0";
        const string secret = "test-secret";
        const string body = "{\"total\":1}";

        // Signature computed with a different secret so verification fails.
        var (signature, timestamp, nonce) = HmacTestSigner.Sign(body, "wrong-secret");

        var secretsService = new Mock<IGitHubOrgSecretsService>(MockBehavior.Strict);
        secretsService
            .Setup(service => service.GetGitHubTokenAsync(owner, "TestData", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GithubSecretResult)secret);

        // Strict mock with no setups: any nonce invocation throws MockException, failing the test.
        var nonceService = new Mock<INonceService>(MockBehavior.Strict);

        var service = new HmacAuthenticationService(
            secretsService.Object,
            nonceService.Object,
            Mock.Of<ILogger<HmacAuthenticationService>>());

        var result = await service.ValidateRequestAsync(
            new HmacAuthContext(owner, repo, platform, branch, signature, timestamp, nonce, body),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.Failure.IsT0); // InvalidSignature

        secretsService.VerifyAll();
        nonceService.VerifyAll();
    }

    [Fact]
    public async Task ValidateRequestAsync_Should_Mark_Nonce_After_Signature_Succeeds()
    {
        const string owner = "localstack-dotnet";
        const string repo = "badge-smith";
        const string platform = "windows";
        const string branch = "feature/iteration0";
        const string secret = "test-secret";
        const string body = "{\"total\":1}";

        var (signature, timestamp, nonce) = HmacTestSigner.Sign(body, secret);

        var callOrder = new List<string>();

        var secretsService = new Mock<IGitHubOrgSecretsService>(MockBehavior.Strict);
        secretsService
            .Setup(service => service.GetGitHubTokenAsync(owner, "TestData", It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("secret"))
            .ReturnsAsync((GithubSecretResult)secret);

        var nonceService = new Mock<INonceService>(MockBehavior.Strict);
        nonceService
            .Setup(service => service.ValidateAndMarkNonceAsync(nonce, It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("nonce"))
            .ReturnsAsync((NonceValidationResult)new ValidNonce(nonce, DateTimeOffset.UtcNow));

        var service = new HmacAuthenticationService(
            secretsService.Object,
            nonceService.Object,
            Mock.Of<ILogger<HmacAuthenticationService>>());

        var result = await service.ValidateRequestAsync(
            new HmacAuthContext(owner, repo, platform, branch, signature, timestamp, nonce, body),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, callOrder.Count);
        Assert.Equal("secret", callOrder[0]);
        Assert.Equal("nonce", callOrder[1]);

        secretsService.VerifyAll();
        nonceService.VerifyAll();
    }
}
