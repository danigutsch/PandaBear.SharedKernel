namespace SharedKernel.Testing.CodeFixRunner.Tests;

/// <summary>Tests the filesystem assertions' type, absence and failure diagnostics.</summary>
public sealed class PathAssertionExtensionsTests
{
    [Fact]
    public async Task Path_assertions_distinguish_files_directories_and_missing_entries()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var file = Path.Combine(workspace.Path, "entry.txt");
        var directory = Path.Combine(workspace.Path, "child");
        var missing = Path.Combine(workspace.Path, "missing");

        // Act
        await File.WriteAllTextAsync(file, string.Empty, TestContext.Current.CancellationToken);
        Directory.CreateDirectory(directory);

        // Assert
        file.ShouldExistAsFile();
        file.ShouldNotExistAsDirectory();
        directory.ShouldExistAsDirectory();
        directory.ShouldNotExistAsFile();
        missing.ShouldNotExistAsFile();
        missing.ShouldNotExistAsDirectory();
    }

    [Fact]
    public async Task File_assertion_failures_report_the_path_and_the_callers_reason()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var file = Path.Combine(workspace.Path, "entry.txt");
        var directory = workspace.Path;
        const string ExpectedReason = "The fixture should include a staged helper.";
        const string UnexpectedReason = "Rollback should remove the staged helper.";
        await File.WriteAllTextAsync(file, string.Empty, TestContext.Current.CancellationToken);
        Action expectFile = () => directory.ShouldExistAsFile(userMessage: ExpectedReason);
        Action rejectFile = () => file.ShouldNotExistAsFile(userMessage: UnexpectedReason);

        // Act
        var missingFile = expectFile.ShouldThrow<Xunit.Sdk.TrueException>();
        var unexpectedFile = rejectFile.ShouldThrow<Xunit.Sdk.TrueException>();

        // Assert
        missingFile.Message.ShouldStartWith(ExpectedReason, StringComparison.Ordinal);
        missingFile.Message.ShouldContain(directory, StringComparison.Ordinal);
        missingFile.Message.ShouldContain("an existing file", StringComparison.Ordinal);
        unexpectedFile.Message.ShouldStartWith(UnexpectedReason, StringComparison.Ordinal);
        unexpectedFile.Message.ShouldContain(file, StringComparison.Ordinal);
        unexpectedFile.Message.ShouldContain("no existing file", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Directory_assertion_failures_report_the_path_and_required_entry_type()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var file = Path.Combine(workspace.Path, "entry.txt");
        var directory = workspace.Path;
        await File.WriteAllTextAsync(file, string.Empty, TestContext.Current.CancellationToken);
        Action expectDirectory = () => file.ShouldExistAsDirectory();
        Action rejectDirectory = () => directory.ShouldNotExistAsDirectory();

        // Act
        var missingDirectory = expectDirectory.ShouldThrow<Xunit.Sdk.TrueException>();
        var unexpectedDirectory = rejectDirectory.ShouldThrow<Xunit.Sdk.TrueException>();

        // Assert
        missingDirectory.Message.ShouldContain(file, StringComparison.Ordinal);
        missingDirectory.Message.ShouldContain("an existing directory", StringComparison.Ordinal);
        unexpectedDirectory.Message.ShouldContain(directory, StringComparison.Ordinal);
        unexpectedDirectory.Message.ShouldContain("no existing directory", StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_path_is_an_argument_error_even_for_an_absence_assertion()
    {
        // Arrange
        Action action = () => string.Empty.ShouldNotExistAsFile();

        // Act
        var exception = action.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("path");
    }
}
