using System.Globalization;
using BadgeSmith.Api.Core.Security;
using BadgeSmith.Api.Core.Security.Contracts;
using BadgeSmith.Api.Tests.Testing;
using BadgeSmith.Api.Tests.Testing.Infrastructure;
using BadgeSmith.Protocol;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BadgeSmith.Api.Tests.Security;

[Trait("Category", TestCategories.Unit)]
public sealed class HmacAuthenticationServiceTests
{
    public static TheoryData<string, string, string> CanonicalVectors => new()
    {
        {
            "feature/iteration0", """
                                  BADGESMITH-HMAC
                                  POST
                                  /tests/results/linux/localstack-dotnet/badgesmith/feature%2Fiteration0
                                  2026-08-07T12:34:56.789Z
                                  nonce-123
                                  49dfeb8e72e7baaa6cea0661ad7b467785356dacf72f6245afe86dc1acac25de
                                  """,
            "sha256=9ca1aa9744b92149e0a4267632db0d6c74a24f86c4d875ac5c4e4d3b3b2ccf15"
        },
        {
            "feature%2Fiteration0", """
                                    BADGESMITH-HMAC
                                    POST
                                    /tests/results/linux/localstack-dotnet/badgesmith/feature%252Fiteration0
                                    2026-08-07T12:34:56.789Z
                                    nonce-123
                                    49dfeb8e72e7baaa6cea0661ad7b467785356dacf72f6245afe86dc1acac25de
                                    """,
            "sha256=c928b7b4fddf4850cd6b61b07855ef12bcea6da95b43e2d60ac2459b587826bc"
        },
        {
            "feature+iteration0", """
                                  BADGESMITH-HMAC
                                  POST
                                  /tests/results/linux/localstack-dotnet/badgesmith/feature%2Biteration0
                                  2026-08-07T12:34:56.789Z
                                  nonce-123
                                  49dfeb8e72e7baaa6cea0661ad7b467785356dacf72f6245afe86dc1acac25de
                                  """,
            "sha256=53a538582455d54f691497f471a36bdfe88f3ce91fee34a09d3491dd33e3c02a"
        },
        {
            "feature iteration0", """
                                  BADGESMITH-HMAC
                                  POST
                                  /tests/results/linux/localstack-dotnet/badgesmith/feature%20iteration0
                                  2026-08-07T12:34:56.789Z
                                  nonce-123
                                  49dfeb8e72e7baaa6cea0661ad7b467785356dacf72f6245afe86dc1acac25de
                                  """,
            "sha256=10c0314d20aee87a2bda429ea2e7bc217416a2b989ae58000d7f9e66394e14a6"
        },
    };

    public static TheoryData<string> MalformedSignatures =>
    [
        "sha256=" + new string('z', 64),
        "sha256=" + new string('0', 63),
        "sha256=" + new string('0', 62),
        "sha256=" + new string('0', 66),
    ];

    public static TheoryData<string> SignedFieldNames =>
    [
        "owner",
        "repo",
        "platform",
        "branch",
        "timestamp",
        "nonce",
        "body",
    ];

    [Theory]
    [MemberData(nameof(CanonicalVectors))]
    public void HmacCanonicalRequest_Should_Build_Literal_Canonical_Text_When_Branch_Requires_Escaping(
        string branch,
        string expectedCanonicalText,
        string expectedSignature)
    {
        const string owner = "LocalStack-DotNet";
        const string repo = "BadgeSmith";
        const string platform = "Linux";
        const string timestamp = " 2026-08-07T12:34:56.789Z ";
        const string nonce = " nonce-123 ";
        const string body = "{\"total\":1,\"platform\":\"Linux\"}";
        const string secret = "super-secret";

        var canonicalText = HmacCanonicalRequest.CreateCanonicalText(platform, owner, repo, branch, timestamp, nonce, body);
        var signature = HmacTestSigner.Sign(owner, repo, platform, branch, body, secret,
            timestamp: new DateTimeOffset(2026, 8, 7, 12, 34, 56, 789, TimeSpan.Zero),
            nonce: "nonce-123").Signature;

        Assert.Equal(expectedCanonicalText, canonicalText);
        Assert.False(canonicalText.EndsWith('\n'));
        Assert.Equal(expectedSignature, signature);
    }

    [Fact]
    public async Task ValidateRequestAsync_Should_Use_Platform_In_Repository_Identifier_When_Signature_Is_Valid()
    {
        const string owner = "localstack-dotnet";
        const string repo = "badge-smith";
        const string platform = "windows";
        const string branch = "feature/iteration0";
        const string secret = "test-secret";
        const string body = "{\"total\":1}";

        var (signature, timestamp, nonce) = HmacTestSigner.Sign(owner, repo, platform, branch, body, secret);
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
    public async Task ValidateRequestAsync_Should_Use_Normalized_Owner_Repo_And_Platform_In_Repository_Identifier_When_Route_Casing_Differs()
    {
        const string owner = "LocalStack-DotNet";
        const string repo = "BadgeSmith";
        const string platform = "Linux";
        const string branch = "Feature/CaseSensitive";
        const string secret = "test-secret";
        const string body = "{\"total\":1}";
        const string expectedRepoIdentifier = "localstack-dotnet/badgesmith/linux/Feature/CaseSensitive";

        var (signature, timestamp, nonce) = HmacTestSigner.Sign(owner, repo, platform, branch, body, secret);
        string? nonceRepoIdentifier = null;

        var secretsService = new Mock<IGitHubOrgSecretsService>(MockBehavior.Strict);
        secretsService
            .Setup(service => service.GetGitHubTokenAsync(owner, "TestData", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GithubSecretResult)secret);

        var nonceService = new Mock<INonceService>(MockBehavior.Strict);
        nonceService
            .Setup(service => service.ValidateAndMarkNonceAsync(nonce, expectedRepoIdentifier, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateTimeOffset, CancellationToken>((_, repoIdentifier, _, _) => nonceRepoIdentifier = repoIdentifier)
            .ReturnsAsync((NonceValidationResult)new ValidNonce(nonce, DateTimeOffset.UtcNow));

        var service = new HmacAuthenticationService(
            secretsService.Object,
            nonceService.Object,
            Mock.Of<ILogger<HmacAuthenticationService>>());

        var result = await service.ValidateRequestAsync(
            new HmacAuthContext(owner, repo, platform, branch, signature, timestamp, nonce, body),
            TestContext.Current.CancellationToken);

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
        var (signature, timestamp, nonce) = HmacTestSigner.Sign(owner, repo, platform, branch, body, "wrong-secret");

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

    [Theory]
    [MemberData(nameof(MalformedSignatures))]
    public async Task ValidateRequestAsync_Should_Return_Invalid_Signature_Without_Marking_Nonce_When_Signature_Is_Malformed(string signature)
    {
        const string owner = "localstack-dotnet";
        const string repo = "badge-smith";
        const string platform = "linux";
        const string branch = "feature/tools";
        const string secret = "test-secret";
        const string body = "{\"total\":1}";
        var (_, timestamp, nonce) = HmacTestSigner.Sign(owner, repo, platform, branch, body, secret);

        var secretsService = new Mock<IGitHubOrgSecretsService>(MockBehavior.Strict);
        secretsService
            .Setup(service => service.GetGitHubTokenAsync(owner, "TestData", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GithubSecretResult)secret);
        var nonceService = new Mock<INonceService>(MockBehavior.Strict);
        var service = new HmacAuthenticationService(
            secretsService.Object,
            nonceService.Object,
            Mock.Of<ILogger<HmacAuthenticationService>>());

        var result = await service.ValidateRequestAsync(
            new HmacAuthContext(owner, repo, platform, branch, signature, timestamp, nonce, body),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.Failure.IsT0);
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

        var (signature, timestamp, nonce) = HmacTestSigner.Sign(owner, repo, platform, branch, body, secret);

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

    [Fact]
    public async Task ValidateRequestAsync_Should_Mark_Trimmed_Nonce_When_Header_Nonce_Has_Surrounding_Whitespace()
    {
        const string owner = "localstack-dotnet";
        const string repo = "badge-smith";
        const string platform = "linux";
        const string branch = "feature/iteration0";
        const string secret = "test-secret";
        const string body = "{\"total\":1}";
        const string canonicalNonce = "nonce-123";
        const string headerNonce = "  nonce-123  ";

        var (signature, timestamp, _) = HmacTestSigner.Sign(owner, repo, platform, branch, body, secret, nonce: canonicalNonce);
        string? storedNonce = null;

        var secretsService = new Mock<IGitHubOrgSecretsService>(MockBehavior.Strict);
        secretsService
            .Setup(service => service.GetGitHubTokenAsync(owner, "TestData", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GithubSecretResult)secret);

        var nonceService = new Mock<INonceService>(MockBehavior.Strict);
        nonceService
            .Setup(service => service.ValidateAndMarkNonceAsync(canonicalNonce, It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateTimeOffset, CancellationToken>((nonce, _, _, _) => storedNonce = nonce)
            .ReturnsAsync((NonceValidationResult)new ValidNonce(canonicalNonce, DateTimeOffset.UtcNow));

        var service = new HmacAuthenticationService(
            secretsService.Object,
            nonceService.Object,
            Mock.Of<ILogger<HmacAuthenticationService>>());

        var result = await service.ValidateRequestAsync(
            new HmacAuthContext(owner, repo, platform, branch, signature, timestamp, headerNonce, body),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(canonicalNonce, storedNonce);

        secretsService.VerifyAll();
        nonceService.VerifyAll();
    }

    [Theory]
    [MemberData(nameof(SignedFieldNames))]
    public async Task ValidateRequestAsync_Should_Return_Invalid_Signature_Without_Marking_Nonce_When_Signed_Field_Changes(string fieldName)
    {
        const string owner = "localstack-dotnet";
        const string repo = "badge-smith";
        const string platform = "linux";
        const string branch = "feature/iteration0";
        const string secret = "test-secret";
        const string body = "{\"total\":1}";

        var fixedTimestamp = DateTimeOffset.UtcNow.AddSeconds(-30);
        var (signature, timestamp, nonce) = HmacTestSigner.Sign(owner, repo, platform, branch, body, secret, timestamp: fixedTimestamp, nonce: "nonce-123");
        var context = fieldName switch
        {
            "owner" => new HmacAuthContext("localstack-dotnet-alt", repo, platform, branch, signature, timestamp, nonce, body),
            "repo" => new HmacAuthContext(owner, "badge-smith-alt", platform, branch, signature, timestamp, nonce, body),
            "platform" => new HmacAuthContext(owner, repo, "windows", branch, signature, timestamp, nonce, body),
            "branch" => new HmacAuthContext(owner, repo, platform, "feature/other", signature, timestamp, nonce, body),
            "timestamp" => new HmacAuthContext(owner, repo, platform, branch, signature,
                DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture).AddSeconds(1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture), nonce, body),
            "nonce" => new HmacAuthContext(owner, repo, platform, branch, signature, timestamp, "nonce-456", body),
            "body" => new HmacAuthContext(owner, repo, platform, branch, signature, timestamp, nonce, "{\"total\":2}"),
            _ => throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, "Unknown signed field."),
        };

        var secretsService = new Mock<IGitHubOrgSecretsService>(MockBehavior.Strict);
        secretsService
            .Setup(service => service.GetGitHubTokenAsync(It.IsAny<string>(), "TestData", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GithubSecretResult)secret);

        var nonceService = new Mock<INonceService>(MockBehavior.Strict);
        var service = new HmacAuthenticationService(
            secretsService.Object,
            nonceService.Object,
            Mock.Of<ILogger<HmacAuthenticationService>>());

        var result = await service.ValidateRequestAsync(context, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.Failure.IsT0);
        secretsService.VerifyAll();
        nonceService.VerifyAll();
    }
}
