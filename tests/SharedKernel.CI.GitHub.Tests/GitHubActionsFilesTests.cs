namespace SharedKernel.CI.GitHub.Tests;

/// <summary>Guards the GitHub Actions output and step summary files.</summary>
public sealed class GitHubActionsFilesTests
{
    [Fact]
    public async Task Outputs_are_appended_as_name_value_lines()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "output");
        await File.WriteAllTextAsync(path, "earlier=1\n", TestContext.Current.CancellationToken);
        var outputs = new GitHubActionsOutputs(path);

        // Act
        await outputs.Write([new("build_required", "true"), new("matrix", "[\"library\"]")], TestContext.Current.CancellationToken);

        // Assert
        var written = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        written.ShouldBe("earlier=1\nbuild_required=true\nmatrix=[\"library\"]\n");
    }

    [Theory]
    [InlineData("", "value")]
    [InlineData("a=b", "value")]
    [InlineData("name\n", "value")]
    [InlineData("name", "line\nbreak")]
    [InlineData("name", "carriage\rreturn")]
    public async Task An_output_that_would_break_the_line_format_is_refused_before_anything_is_written(string name, string value)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "output");
        var outputs = new GitHubActionsOutputs(path);

        // Act
        var write = () => outputs.Write([new("first", "ok"), new(name, value)], TestContext.Current.CancellationToken);

        // Assert
        await write.ShouldThrow<ArgumentException>();
        File.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public async Task The_summary_appends_markdown()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "summary.md");
        var summary = new GitHubActionsStepSummary(path);

        // Act
        await summary.Append("## one\n", TestContext.Current.CancellationToken);
        await summary.Append("## two\n", TestContext.Current.CancellationToken);

        // Assert
        var written = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        written.ShouldBe("## one\n## two\n");
    }

    [Fact]
    public void Relative_file_paths_are_refused()
    {
        // Act
        var outputs = () => new GitHubActionsOutputs("relative/output");
        var summary = () => new GitHubActionsStepSummary("relative/summary.md");

        // Assert
        outputs.ShouldThrow<ArgumentException>();
        summary.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public async Task Release_assets_download_from_the_release_page_of_the_tag()
    {
        // Act
        var uri = await new GitHubReleaseAssets("owner/name").Locate("v1.0.0-alpha.1", "PandaBear.SharedKernel.CI.1.0.0-alpha.1.nupkg", TestContext.Current.CancellationToken);

        // Assert
        uri.ToString().ShouldBe("https://github.com/owner/name/releases/download/v1.0.0-alpha.1/PandaBear.SharedKernel.CI.1.0.0-alpha.1.nupkg");
    }
}
