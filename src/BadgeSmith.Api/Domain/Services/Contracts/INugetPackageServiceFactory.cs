namespace BadgeSmith.Api.Domain.Services.Contracts;

internal interface INugetPackageServiceFactory
{
    public INuGetPackageService NuGetPackageService { get; }
}
