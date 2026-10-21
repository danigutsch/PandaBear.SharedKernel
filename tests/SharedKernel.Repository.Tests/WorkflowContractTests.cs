using PandaBear.Repository.Tests.Conventions;

namespace PandaBear.Repository.Tests;

public sealed class WorkflowContractTests
{
    [Theory]
    [InlineData("tools/SharedKernel.Repository.Tool", "select")]
    [InlineData("tools/SharedKernel.Repository.Tool", "validate")]
    [InlineData("tools/SharedKernel.Versioning.Tool", "calculate-release")]
    [InlineData("tools/SharedKernel.Versioning.Tool", "create-candidate")]
    [InlineData("tools/SharedKernel.Versioning.Tool", "consume-candidate")]
    [InlineData("tools/SharedKernel.Documentation.Tool", "generate")]
    public void Active_ci_delegates_repository_operations_to_existing_dotnet_tools(string project, string command)
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"))
            .Replace("\\\n", string.Empty, StringComparison.Ordinal);
        var commands = workflow.Split('\n').Select(static line => string.Join(' ', line.Trim()
            .Replace("run: ", string.Empty, StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries)));

        // Assert
        commands.ShouldContain(line => line.StartsWith("dotnet run --project " + project + " ", StringComparison.Ordinal)
            && line.Contains("-- " + command, StringComparison.Ordinal));
    }

    [Fact]
    public void Active_workflows_do_not_execute_archived_extraction_scripts()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var workflows = Directory.GetFiles(Path.Combine(root, ".github", "workflows"), "*.y*ml");

        // Assert
        workflows.ShouldNotBeEmpty();
        foreach (var workflow in workflows)
        {
            File.ReadAllText(workflow).ShouldNotContain("provenance/extraction-tools/", StringComparison.Ordinal);
        }
    }
}
