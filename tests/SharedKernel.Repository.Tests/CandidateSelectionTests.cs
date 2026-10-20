using PandaBear.Repository.Tool;

namespace PandaBear.Repository.Tests;

public static class CandidateSelectionTests
{
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
}
