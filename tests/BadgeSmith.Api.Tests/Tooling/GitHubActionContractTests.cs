using BadgeSmith.Api.Tests.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Tooling;

[Trait("Category", TestCategories.Unit)]
public sealed class GitHubActionContractTests
{
    [Fact]
    public async Task Update_Test_Badge_Action_Should_Resolve_Sdk_And_Tool_From_Action_Path()
    {
        var action = await ReadRepositoryFileAsync(
            TestContext.Current.CancellationToken,
            ".github", "workflows", "update-test-badge", "action.yml");

        Assert.Contains(
            "uses: actions/setup-dotnet@v6",
            action,
            StringComparison.Ordinal);
        Assert.Contains("global-json-file: ${{ github.action_path }}/../../../global.json", action, StringComparison.Ordinal);
        Assert.Contains("$env:BADGESMITH_ACTION_PATH/../../../tools/badgesmith.cs", action, StringComparison.Ordinal);
        Assert.Contains("$BADGESMITH_ACTION_PATH/../../../tools/badgesmith.cs", action, StringComparison.Ordinal);
        Assert.DoesNotContain("github.workspace", action, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workflow_And_Composite_Actions_Should_Not_Interpolate_Expressions_Inside_Run_Scripts()
    {
        string[][] actionPaths =
        [
            [".github", "workflows", "ci-cd.yml"],
            [".github", "workflows", "update-test-badge", "action.yml"],
            [".github", "workflows", "run-dotnet-tests", "action.yml"],
        ];

        foreach (var actionPath in actionPaths)
        {
            var action = await ReadRepositoryFileAsync(TestContext.Current.CancellationToken, actionPath);
            var scripts = ExtractRunScripts(action);
            Assert.NotEmpty(scripts);
            foreach (var script in scripts)
            {
                Assert.DoesNotContain("${{", script, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task Workflows_Should_Pin_External_Actions_Runners_Node_And_Cdk()
    {
        string[][] workflowPaths =
        [
            [".github", "workflows", "ci-cd.yml"],
            [".github", "workflows", "deploy.yml"],
            [".github", "workflows", "update-test-badge", "action.yml"],
        ];

        var workflows = await Task.WhenAll(
            workflowPaths.Select(path => ReadRepositoryFileAsync(TestContext.Current.CancellationToken, path)));

        foreach (var workflow in workflows)
        {
            Assert.DoesNotContain("ubuntu-latest", workflow, StringComparison.Ordinal);

            foreach (var line in workflow.Split('\n'))
            {
                var actionReference = line.Trim().TrimStart('-').TrimStart();
                if (!actionReference.StartsWith("uses: ", StringComparison.Ordinal)
                    || actionReference.StartsWith("uses: ./", StringComparison.Ordinal))
                {
                    continue;
                }

                var version = actionReference.Split('#', 2)[0].Split('@', 2)[1].Trim();
                Assert.Matches("^v[0-9]+$", version);
            }
        }

        var deployWorkflow = workflows[1];
        Assert.Contains("node-version: '24'", deployWorkflow, StringComparison.Ordinal);
        Assert.Contains("aws-cdk@2.1135.1", deployWorkflow, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_Dotnet_Tests_Action_Should_Resolve_Tool_From_Workspace_Environment()
    {
        var action = await ReadRepositoryFileAsync(
            TestContext.Current.CancellationToken,
            ".github", "workflows", "run-dotnet-tests", "action.yml");

        Assert.Contains("BADGESMITH_TOOL_PATH: ${{ github.workspace }}/tools/badgesmith.cs", action, StringComparison.Ordinal);
        Assert.Contains("$env:BADGESMITH_TOOL_PATH", action, StringComparison.Ordinal);
        Assert.Contains("$BADGESMITH_TOOL_PATH", action, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ci_Workflow_Should_Use_File_System_Safe_Pr_Artifact_Name()
    {
        var workflow = await ReadRepositoryFileAsync(
            TestContext.Current.CancellationToken,
            ".github", "workflows", "ci-cd.yml");

        Assert.Contains("format('lambda-zip-pr-{0}', github.event.pull_request.number)", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("format('lambda-zip-{0}', github.head_ref || github.ref_name)", workflow, StringComparison.Ordinal);
    }

    private static Task<string> ReadRepositoryFileAsync(CancellationToken cancellationToken, params string[] segments)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllTextAsync(Path.Combine([root, .. segments]), cancellationToken);
    }

    private static List<string> ExtractRunScripts(string yaml)
    {
        var scripts = new List<string>();
        var currentScript = new List<string>();
        var runIndent = -1;

        foreach (var line in yaml.Split('\n'))
        {
            var trimmedLine = line.TrimStart();
            var indent = line.Length - trimmedLine.Length;
            if (trimmedLine.Equals("run: |", StringComparison.Ordinal))
            {
                runIndent = indent;
                currentScript.Clear();
                continue;
            }

            if (runIndent >= 0 && trimmedLine.Length > 0 && indent <= runIndent)
            {
                scripts.Add(string.Join('\n', currentScript));
                runIndent = -1;
            }

            if (runIndent >= 0)
            {
                currentScript.Add(line);
            }
        }

        if (runIndent >= 0)
        {
            scripts.Add(string.Join('\n', currentScript));
        }

        return scripts;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
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
