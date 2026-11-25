namespace SharedKernel.CI.Tests;

/// <summary>Tests the forge-neutral run output file and step summary file.</summary>
public sealed class RunOutputFilesTests
{
    [Fact]
    public async Task Outputs_are_appended_as_name_value_lines_ending_in_a_line_feed()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "output");
        await File.WriteAllTextAsync(path, "earlier=1\n", TestContext.Current.CancellationToken);
        var outputs = new KeyValueOutputFile(path);

        // Act
        await outputs.Write([new("packages", "pkg:nuget/a@1,pkg:nuget/b@2"), new("empty", "")], TestContext.Current.CancellationToken);

        // Assert
        var written = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        written.ShouldBe("earlier=1\npackages=pkg:nuget/a@1,pkg:nuget/b@2\nempty=\n");
    }

    [Theory]
    [InlineData("", "value")]
    [InlineData("a=b", "value")]
    [InlineData("name\r", "value")]
    [InlineData("name", "line\nbreak")]
    [InlineData("name", "injected\nother=1")]
    public async Task An_output_that_would_break_the_line_format_is_refused_before_anything_is_written(string name, string value)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "output");
        var outputs = new KeyValueOutputFile(path);

        // Act
        var failure = await Record.ExceptionAsync(() => outputs.Write([new("first", "ok"), new(name, value)], TestContext.Current.CancellationToken));

        // Assert
        failure.ShouldBeOfType<ArgumentException>().ParamName.ShouldBe("outputs");
        File.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public async Task The_summary_appends_markdown_without_a_byte_order_mark()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "summary.md");
        var summary = new MarkdownSummaryFile(path);

        // Act
        await summary.Append("## one\n", TestContext.Current.CancellationToken);
        await summary.Append("## two\n", TestContext.Current.CancellationToken);

        // Assert
        var written = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        written.ShouldBe("## one\n## two\n"u8.ToArray());
    }

    [Fact]
    public void Both_files_need_a_fully_qualified_path()
    {
        // Act
        var output = Record.Exception(() => new KeyValueOutputFile("relative/output"));
        var summary = Record.Exception(() => new MarkdownSummaryFile("relative/summary.md"));
        var missing = Record.Exception(() => new KeyValueOutputFile(null!));

        // Assert
        output.ShouldBeOfType<ArgumentException>().Message.ShouldStartWith("The output file must be a fully qualified path.", StringComparison.Ordinal);
        summary.ShouldBeOfType<ArgumentException>().Message.ShouldStartWith("The summary file must be a fully qualified path.", StringComparison.Ordinal);
        missing.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("path");
    }
}
