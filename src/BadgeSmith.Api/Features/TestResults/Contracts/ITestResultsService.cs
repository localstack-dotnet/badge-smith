using BadgeSmith.Api.Features.TestResults.Models;

namespace BadgeSmith.Api.Features.TestResults.Contracts;

/// <summary>
/// Business service for managing test results. Handles payload validation, enrichment
/// </summary>
internal interface ITestResultsService
{
    public Task<TestResultStorageResult> StoreTestResultAsync(StoreTestResultRequest testResultRequest, CancellationToken ct = default);

    public Task<TestResultQueryResult> GetLatestTestResultAsync(string owner, string repo, string platform, string branch, CancellationToken ct = default);
}
