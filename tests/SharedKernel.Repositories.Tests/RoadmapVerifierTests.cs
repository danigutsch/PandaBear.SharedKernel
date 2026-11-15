namespace PandaBear.Repositories.Tests;

/// <summary>Engine tests split from the command-line tests in RepoConfigToolCoverageTests.</summary>
public sealed class RoadmapVerifierTests
{
    [Fact]
    public void Try_parse_JSON_file_reports_read_failures_as_issues()
    {
        // Arrange
        List<VerificationIssue> issues = [];

        // Act
        var document = RoadmapVerifier.TryParseJsonFile("/repository", "/repository/roadmap/config.json", issues, _ => throw new IOException("simulated read failure"));

        // Assert
        document.ShouldBeNull();
        issues.Count.ShouldBe(1);
        issues[0].Path.ShouldBe("roadmap/config.json");
        issues[0].Message.ShouldBe("Unable to read JSON: simulated read failure");
    }
}
