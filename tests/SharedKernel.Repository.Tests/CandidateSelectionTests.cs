using PandaBear.Repository.Tool;

namespace PandaBear.Repository.Tests;

public static class CandidateSelectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void Selection_appends_github_candidate_output_for_the_selected_repository(bool hasProjects)
    {
        // Arrange
        using var repository = new RepositoryTestScope();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var githubOutput = Path.Combine(repository.Root, "github-output.txt");
        var previousOutput = "existing=value" + Environment.NewLine;
        File.WriteAllText(githubOutput, previousOutput);
        if (!hasProjects)
        {
            File.WriteAllText(Path.Combine(repository.Root, "SharedKernel.slnx"), "<Solution />");
        }

        // Act
        var result = RepositoryToolApplication.Run(
            ["select", "--github-output", "github-output.txt", "--root", repository.Root], output, error);

        // Assert
        result.ShouldBe(0);
        var expected = hasProjects ? "true" : "false";
        File.ReadAllText(githubOutput).ShouldStartWith(previousOutput + "candidate_required=" + expected + Environment.NewLine, StringComparison.Ordinal);
        File.ReadAllText(Path.Combine(repository.Root, "TestResults", "selection", "candidate-required.txt")).ShouldBe(expected);
    }

    [Fact]
    public static void Selection_records_candidate_requirement_without_git_history()
    {
        // Arrange
        using var repository = new RepositoryTestScope();
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var result = RepositoryToolApplication.Run(["select", "--root", repository.Root], output, error);

        // Assert
        result.ShouldBe(0);
        File.ReadAllText(Path.Combine(repository.Root, "TestResults", "selection", "candidate-required.txt")).ShouldBe("true");
    }

    [Theory]
    [InlineData("docs/release-policy.md", false)]
    [InlineData("README.md", false)]
    [InlineData("src/Core/README.md", true)]
    [InlineData("Directory.Build.props", true)]
    [InlineData("unknown-input", true)]
    public static void Package_inputs_require_candidates_while_repository_docs_do_not(string path, bool required)
    {
        // Arrange
        using var repository = new RepositoryTestScope();

        // Act
        var result = ProjectSelector.Select(repository.Root, [path]);

        // Assert
        (result.BuildProjects.Length > 0).ShouldBe(required);
    }
    [Fact]
    public static void Newly_added_hosted_tests_are_emitted_into_the_workflow_matrix()
    {
        // Arrange
        using var repository = new RepositoryTestScope();
        repository.AddProject("tests/NewHosted/NewHosted.csproj", """<Project Sdk="Aspire.AppHost.Sdk"><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>""");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var githubOutput = Path.Combine(repository.Root, "github-output.txt");

        // Act
        var result = RepositoryToolApplication.Run(["select", "--root", repository.Root, "--github-output", githubOutput], output, error);

        // Assert
        result.ShouldBe(0);
        File.ReadAllText(githubOutput).ShouldContain("hosted_required=true", StringComparison.Ordinal);
        File.ReadAllText(githubOutput).ShouldContain("NewHosted.csproj", StringComparison.Ordinal);
    }
}
