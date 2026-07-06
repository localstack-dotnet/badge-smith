namespace BadgeSmith.Tools.Infrastructure;

internal sealed class RepositoryPaths
{
    public RepositoryPaths(string? startDirectory = null)
    {
        RepositoryRoot = FindRepositoryRoot(startDirectory ?? Directory.GetCurrentDirectory());
        ArtifactsDirectory = Path.Combine(RepositoryRoot, "artifacts");
    }

    public string RepositoryRoot { get; }

    public string ArtifactsDirectory { get; }

    public string ResolveFromRoot(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        return Path.Combine([RepositoryRoot, .. segments]);
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) && (File.Exists(gitPath) || Directory.Exists(gitPath)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find BadgeSmith repository root.");
    }
}
