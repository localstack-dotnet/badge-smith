using BadgeSmith.Api.Domain.Services.Contracts;
using BadgeSmith.Api.Domain.Services.Package;
using BadgeSmith.Api.Infrastructure.Caching;
using BadgeSmith.Api.Infrastructure.Http;
using BadgeSmith.Api.Infrastructure.Observability;

namespace BadgeSmith.Api.Domain.Services.Nuget;

internal class NuGetPackageServiceFactory : INugetPackageServiceFactory
{
    private static readonly Lazy<INuGetPackageService> NuGetPackageServiceLazy = new(CreateNuGetPackageService);

    public INuGetPackageService NuGetPackageService => NuGetPackageServiceLazy.Value;

    private static NuGetPackageService CreateNuGetPackageService()
    {
        var logger = LoggerFactory.CreateLogger<NuGetPackageService>();
        var httpClient = HttpClientFactory.CreateNuGetClient();
        var nuGetVersionService = new NuGetVersionService();

        var cache = new MemoryAppCache();
        return new NuGetPackageService(nuGetVersionService, logger, httpClient, cache);
    }
}
