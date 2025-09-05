using BadgeSmith.Api.Domain.Services.Contracts;
using BadgeSmith.Api.Domain.Services.Package;
using BadgeSmith.Api.Infrastructure.Caching;
using BadgeSmith.Api.Infrastructure.Http;
using BadgeSmith.Api.Infrastructure.Observability;

namespace BadgeSmith.Api.Domain.Services.GitHub;

internal class GitHubPackageServiceFactory : IGitHubPackageServiceFactory
{
    private static readonly Lazy<GitHubPackageService> GitHubPackageServiceLazy = new(CreateGitHubPackageService);

    public GitHubPackageService GitHubPackageService => GitHubPackageServiceLazy.Value;

    private static GitHubPackageService CreateGitHubPackageService()
    {
        var githubClient = HttpClientFactory.CreateGithubClient();
        var nuGetVersionService = new NuGetVersionService();
        var memoryAppCache = new MemoryAppCache();
        var gitHubPackageLogger = LoggerFactory.CreateLogger<GitHubPackageService>();

        return new GitHubPackageService(githubClient, nuGetVersionService, memoryAppCache, gitHubPackageLogger);
    }
}
