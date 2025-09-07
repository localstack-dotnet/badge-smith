using BadgeSmith.Api.Features.TestResults.Models;

namespace BadgeSmith.Api.Features.TestResults.Contracts;

/// <summary>
/// Business service for managing test results.
/// Handles payload validation, enrichment, and coordinates with the repository layer.
/// </summary>
internal interface ITestResultsService
{
    /// <summary>
    /// Stores a test result from a CI / CD pipeline with full validation and enrichment.
    /// </summary>
    /// <param name="testResultRequest">Test result payload from request body and route values</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Storage result indicating success or specific failure type</returns>
    public Task<TestResultStorageResult> StoreTestResultAsync(StoreTestResultRequest testResultRequest, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the latest test result for a specific platform/branch combination.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="platform">Platform (linux/windows/macos)</param>
    /// <param name="branch">Branch name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Query result containing the latest test result or failure information</returns>
    public Task<TestResultQueryResult> GetLatestTestResultAsync(string owner, string repo, string platform, string branch, CancellationToken ct = default);
}
