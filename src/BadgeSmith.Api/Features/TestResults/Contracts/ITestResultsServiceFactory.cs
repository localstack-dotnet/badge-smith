namespace BadgeSmith.Api.Features.TestResults.Contracts;

internal interface ITestResultsServiceFactory
{
    public ITestResultsService TestResultsService { get; }
}
