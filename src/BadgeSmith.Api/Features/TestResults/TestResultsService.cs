using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BadgeSmith.Api.Core;
using BadgeSmith.Api.Features.TestResults.Contracts;
using BadgeSmith.Api.Features.TestResults.Models;
using Microsoft.Extensions.Logging;
using TestResultQueryResult = BadgeSmith.Api.Features.TestResults.Models.TestResultQueryResult;
using TestResultStorageResult = BadgeSmith.Api.Features.TestResults.Models.TestResultStorageResult;

namespace BadgeSmith.Api.Features.TestResults;

internal sealed class TestResultsService : ITestResultsService
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly ILogger<TestResultsService> _logger;

    public TestResultsService(IAmazonDynamoDB dynamoDb, string tableName, ILogger<TestResultsService> logger)
    {
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TestResultStorageResult> StoreTestResultAsync(StoreTestResultRequest testResultRequest, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultsService)}.{nameof(StoreTestResultAsync)}");

        ArgumentNullException.ThrowIfNull(testResultRequest);
        ArgumentNullException.ThrowIfNull(testResultRequest.Payload);

        var payload = testResultRequest.Payload;

        if (!TryValidateTestPayload(payload, out var validationFailure))
        {
            return validationFailure;
        }

        var ownerNormalized = testResultRequest.Owner.ToLowerInvariant();
        var repoNormalized = testResultRequest.Repo.ToLowerInvariant();
        var platformNormalized = testResultRequest.Platform.ToLowerInvariant();
        var branchNormalized = testResultRequest.Branch.ToLowerInvariant();

        var entity = TestResultEntity.FromPayload(ownerNormalized, repoNormalized, platformNormalized, branchNormalized, payload, DateTimeOffset.UtcNow);

        _logger.LogInformation("Storing test result for {Owner}/{Repo} on {Platform}/{Branch}: {Passed}P/{Failed}F/{Skipped}S",
            ownerNormalized, repoNormalized, platformNormalized, branchNormalized,
            payload.Passed, payload.Failed, payload.Skipped);

        try
        {
            var putRequest = MapToDynamoDbItem(entity, _tableName);
            await _dynamoDb.PutItemAsync(putRequest, ct).ConfigureAwait(false);

            _logger.LogInformation("Successfully stored test result {RunId} for {Owner}/{Repo}", entity.RunId, entity.Owner, entity.Repo);

            return new TestResultStored(entity.RunId, entity.CreatedAt);
        }
        catch (ConditionalCheckFailedException ex)
        {
            _logger.LogWarning(ex, "Test result {RunId} already exists for {Owner}/{Repo}", entity.RunId, entity.Owner, entity.Repo);
            return new DuplicateTestResult($"Test result with run_id '{entity.RunId}' already exists");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store test result {RunId} for {Owner}/{Repo}", entity.RunId, entity.Owner, entity.Repo);
            return new Error($"Failed to store test result: {ex.Message}");
        }
    }

    public async Task<TestResultQueryResult> GetLatestTestResultAsync(string owner, string repo, string platform, string branch, CancellationToken ct = default)
    {
        using var activity = BadgeSmithApiActivitySource.ActivitySource.StartActivity($"{nameof(TestResultsService)}.{nameof(GetLatestTestResultAsync)}");

        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);

        var ownerNormalized = owner.ToLowerInvariant();
        var repoNormalized = repo.ToLowerInvariant();
        var platformNormalized = platform.ToLowerInvariant();
        var branchNormalized = branch.ToLowerInvariant();

        _logger.LogDebug("Querying latest test result for {Owner}/{Repo} on {Platform}/{Branch}", ownerNormalized, repoNormalized, platformNormalized, branchNormalized);

        var gsi1Pk = $"LATEST#{owner}#{repo}#{platform}#{branch}";

        var queryRequest = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>(StringComparer.OrdinalIgnoreCase)
            {
                [":gsi1pk"] = new(gsi1Pk),
            },
            ScanIndexForward = false,
            Limit = 1,
        };

        var response = await _dynamoDb.QueryAsync(queryRequest, ct).ConfigureAwait(false);

        if (response.Items == null || response.Items.Count == 0)
        {
            _logger.LogDebug("No test results found for {Owner}/{Repo} on {Platform}/{Branch}", owner, repo, platform, branch);
            return new TestResultNotFound($"No test results found for {owner}/{repo} on {platform}/{branch}");
        }

        var item = response.Items[0];
        var entity = MapFromDynamoDbItem(item);

        _logger.LogDebug("Retrieved latest test result {RunId} for {Owner}/{Repo}", entity.RunId, entity.Owner, entity.Repo);

        return entity;
    }

    private static bool TryValidateTestPayload(TestResultPayload payload, out InvalidTestPayload? error)
    {
        error = null;

        // Validate required fields
        if (string.IsNullOrWhiteSpace(payload.Platform))
        {
            error = new InvalidTestPayload("Platform is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(payload.RunId))
        {
            error = new InvalidTestPayload("RunId is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(payload.Commit))
        {
            error = new InvalidTestPayload("Commit is required");
            return false;
        }

        // Validate test counts
        if (payload.Total < 0 || payload.Passed < 0 || payload.Failed < 0 || payload.Skipped < 0)
        {
            error = new InvalidTestPayload("Test counts cannot be negative");
            return false;
        }

        if (payload.Passed + payload.Failed + payload.Skipped != payload.Total)
        {
            error = new InvalidTestPayload("Test counts do not add up to total");
            return false;
        }

        // Validate URLs
        if (!Uri.TryCreate(payload.UrlHtml, UriKind.Absolute, out _))
        {
            error = new InvalidTestPayload("Invalid url_html format");
            return false;
        }

        if (!Uri.TryCreate(payload.WorkflowRunUrl, UriKind.Absolute, out _))
        {
            error = new InvalidTestPayload("Invalid workflow_run_url format");
            return false;
        }

        return true;
    }

    private static PutItemRequest MapToDynamoDbItem(TestResultEntity entity, string tableName)
    {
        return new PutItemRequest
        {
            TableName = tableName,
            Item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["PK"] = new(entity.Pk),
                ["SK"] = new(entity.Sk),
                ["GSI1PK"] = new(entity.Gsi1Pk),
                ["GSI1SK"] = new(entity.Gsi1Sk),
                ["Owner"] = new(entity.Owner),
                ["Repo"] = new(entity.Repo),
                ["Platform"] = new(entity.Platform),
                ["Branch"] = new(entity.Branch),
                ["Passed"] = new()
                {
                    N = entity.Passed.ToString(CultureInfo.InvariantCulture),
                },
                ["Failed"] = new()
                {
                    N = entity.Failed.ToString(CultureInfo.InvariantCulture),
                },
                ["Skipped"] = new()
                {
                    N = entity.Skipped.ToString(CultureInfo.InvariantCulture),
                },
                ["Total"] = new()
                {
                    N = entity.Total.ToString(CultureInfo.InvariantCulture),
                },
                ["Timestamp"] = new(entity.Timestamp.ToString("O")),
                ["Commit"] = new(entity.Commit),
                ["RunId"] = new(entity.RunId),
                ["UrlHtml"] = new(entity.UrlHtml),
                ["WorkflowRunUrl"] = new(entity.WorkflowRunUrl),
                ["CreatedAt"] = new(entity.CreatedAt.ToString("O")),
                ["TTL"] = new()
                {
                    N = entity.Ttl.ToString(CultureInfo.InvariantCulture),
                },
            },
            ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)",
        };
    }

    private static TestResultEntity MapFromDynamoDbItem(Dictionary<string, AttributeValue> item)
    {
        return new TestResultEntity(
            Pk: item["PK"].S,
            Sk: item["SK"].S,
            Gsi1Pk: item["GSI1PK"].S,
            Gsi1Sk: item["GSI1SK"].S,
            Owner: item["Owner"].S,
            Repo: item["Repo"].S,
            Platform: item["Platform"].S,
            Branch: item["Branch"].S,
            Passed: int.Parse(item["Passed"].N, CultureInfo.InvariantCulture),
            Failed: int.Parse(item["Failed"].N, CultureInfo.InvariantCulture),
            Skipped: int.Parse(item["Skipped"].N, CultureInfo.InvariantCulture),
            Total: int.Parse(item["Total"].N, CultureInfo.InvariantCulture),
            Timestamp: DateTimeOffset.Parse(item["Timestamp"].S, CultureInfo.InvariantCulture),
            Commit: item["Commit"].S,
            RunId: item["RunId"].S,
            UrlHtml: item["UrlHtml"].S,
            WorkflowRunUrl: item["WorkflowRunUrl"].S,
            CreatedAt: DateTimeOffset.Parse(item["CreatedAt"].S, CultureInfo.InvariantCulture),
            Ttl: long.Parse(item["TTL"].N, CultureInfo.InvariantCulture)
        );
    }
}
