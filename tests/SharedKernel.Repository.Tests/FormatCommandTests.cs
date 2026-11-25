namespace SharedKernel.Repository.Tests;

/// <summary>Verifies the checkout and file boundaries of <c>ci verify-format</c>.</summary>
public static class FormatCommandTests
{
    private const string BaseRevision = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public static async Task Selective_format_keeps_a_path_with_spaces_and_omits_deleted_files()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        repository.WriteFile("src/Core/Changed name.cs", "namespace Core;");
        var processes = new ProcessLog(SelectionTestFiles.Git("src/Core/Deleted.cs\0src/Core/Changed name.cs\0"));
        var selection = await RepositoryToolInvocation.Run(repository.Root, ["select", "--base", BaseRevision], processes.Run, ct);
        selection.ExitCode.ShouldBe(0, selection.Error);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["verify-format", "--report", "TestResults/format"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var format = processes.Calls.Where(static call => call.FileName == "dotnet").ShouldHaveSingleItem();
        format.Arguments.ShouldBe([
            "format", Path.Combine(repository.Root, "TestResults", "selection", "affected.build.slnf"),
            "--verify-no-changes", "--no-restore", "--report", Path.Combine(repository.Root, "TestResults", "format"),
            "--include", "./src/Core/Changed name.cs",
        ]);
    }

    [Fact]
    public static async Task Full_validation_formats_the_whole_solution()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog();
        var selection = await RepositoryToolInvocation.Run(repository.Root, ["select"], processes.Run, ct);
        selection.ExitCode.ShouldBe(0, selection.Error);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["verify-format"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var format = processes.Calls.Where(static call => call.FileName == "dotnet").ShouldHaveSingleItem();
        format.Arguments.ShouldBe(["format", "SharedKernel.slnx", "--verify-no-changes", "--no-restore"]);
    }

    [Theory]
    [InlineData("", "The validation plan requires no project formatting.")]
    [InlineData("src/Core/Deleted.cs\0", "No changed files remain in the checkout to format.")]
    public static async Task A_plan_without_existing_changed_files_skips_the_formatter(string paths, string message)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(SelectionTestFiles.Git(paths));
        var selection = await RepositoryToolInvocation.Run(repository.Root, ["select", "--base", BaseRevision], processes.Run, ct);
        selection.ExitCode.ShouldBe(0, selection.Error);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["verify-format"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain(message, StringComparison.Ordinal);
        processes.Calls.Where(static call => call.FileName == "dotnet").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("other-revision", BaseRevision, "The formatting plan revision does not match this checkout.")]
    [InlineData("fixture", "invalid", "Selective formatting requires a full Git comparison base.")]
    public static async Task An_unanchored_plan_fails_before_starting_the_formatter(string revision, string comparisonBase, string message)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(SelectionTestFiles.Git("src/Core/Changed.cs\0"));
        var selection = await RepositoryToolInvocation.Run(repository.Root, ["select", "--base", BaseRevision], processes.Run, ct);
        selection.ExitCode.ShouldBe(0, selection.Error);
        var directory = Path.Combine(repository.Root, "TestResults", "selection");
        var plan = await SelectionPlanFile.Read(directory, ct);
        await SelectionPlanFile.Write(plan with { Revision = revision, ComparisonBase = comparisonBase }, directory, ct);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["verify-format"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(message, StringComparison.Ordinal);
        processes.Calls.Where(static call => call.FileName == "dotnet").ShouldBeEmpty();
    }

    [Fact]
    public static async Task A_changed_file_manifest_mismatch_fails_before_starting_the_formatter()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(SelectionTestFiles.Git("src/Core/Changed.cs\0"));
        var selection = await RepositoryToolInvocation.Run(repository.Root, ["select", "--base", BaseRevision], processes.Run, ct);
        selection.ExitCode.ShouldBe(0, selection.Error);
        repository.WriteFile("TestResults/selection/changed-paths.nul", "src/Core/Other.cs\0");

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["verify-format"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("The formatting plan paths do not match the Git comparison.", StringComparison.Ordinal);
        processes.Calls.Where(static call => call.FileName == "dotnet").ShouldBeEmpty();
    }
}
