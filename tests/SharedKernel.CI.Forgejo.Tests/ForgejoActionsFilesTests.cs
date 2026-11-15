namespace PandaBear.CI.Forgejo.Tests;

/// <summary>Guards the Forgejo Actions output and step summary files.</summary>
public sealed class ForgejoActionsFilesTests
{
    [Fact]
    public async Task Outputs_are_appended_as_name_value_lines()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "output");
        var outputs = new ForgejoActionsOutputs(path);

        // Act
        await outputs.Write([new("build_required", "false"), new("matrix", "[]")], TestContext.Current.CancellationToken);

        // Assert
        var written = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        written.ShouldBe("build_required=false\nmatrix=[]\n");
    }

    [Fact]
    public async Task A_multi_line_value_is_refused()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var outputs = new ForgejoActionsOutputs(Path.Combine(directory.Path, "output"));
        Func<Task> write = () => outputs.Write([new("name", "two\nlines")], TestContext.Current.CancellationToken);

        // Act
        var exception = await write.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("outputs");
    }

    [Fact]
    public async Task The_summary_appends_markdown()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "summary.md");
        var summary = new ForgejoActionsStepSummary(path);

        // Act
        await summary.Append("| a |\n", TestContext.Current.CancellationToken);
        await summary.Append("| b |\n", TestContext.Current.CancellationToken);

        // Assert
        var written = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        written.ShouldBe("| a |\n| b |\n");
    }
}
