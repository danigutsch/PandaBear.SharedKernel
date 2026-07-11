using System.Globalization;

namespace SharedKernel.Testing.CodeFixRunner.Tests;

public sealed class CodeFixRunEngineTests : IDisposable
{
    private readonly TemporaryProjectDirectory projectDirectory = TemporaryProjectDirectory.Create();

    [Fact]
    public async Task Run_applies_SKTEST006_code_fix_to_project_file()
    {
        // Arrange
        var projectPath = Path.Combine(projectDirectory.Path, "Sample.Tests.csproj");
        var sourcePath = Path.Combine(projectDirectory.Path, "SampleTests.cs");
        await File.WriteAllTextAsync(projectPath, CodeFixRunnerTestProject.ProjectFile, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(sourcePath, CodeFixRunnerTestProject.SourceFile, TestContext.Current.CancellationToken);

        var options = new CodeFixRunnerOptions(projectPath, "SKTEST006");
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var fixedCount = await CodeFixRunEngine.Run(options, error);
        var updatedSource = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);

        // Assert
        (fixedCount).ShouldBe(1);
        (updatedSource).ShouldContain("using SharedKernel.Testing.Assertions;", StringComparison.Ordinal);
        (updatedSource).ShouldContain("(true).ShouldBeTrue()", StringComparison.Ordinal);
        (updatedSource).ShouldNotContain("Xunit.Assert.True", StringComparison.Ordinal);
        (string.IsNullOrWhiteSpace(error.ToString())).ShouldBeTrue(error.ToString());
    }

    [Fact]
    public async Task Run_applies_default_SKTEST004_code_fix_to_project_file()
    {
        // Arrange
        var projectPath = Path.Combine(projectDirectory.Path, "Sample.Tests.csproj");
        var sourcePath = Path.Combine(projectDirectory.Path, "SampleTests.cs");
        var helperPath = Path.Combine(projectDirectory.Path, "SampleTestsHelpers.cs");
        await File.WriteAllTextAsync(projectPath, CodeFixRunnerTestProject.ProjectFile, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(sourcePath, CodeFixRunnerTestProject.HelperSourceFile, TestContext.Current.CancellationToken);

        var options = CodeFixRunnerOptions.Parse([projectPath]).ShouldNotBeNull();
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var fixedCount = await CodeFixRunEngine.Run(options, error);

        // Assert
        fixedCount.ShouldBe(1);
        var updatedSource = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);
        var helperSource = await File.ReadAllTextAsync(helperPath, TestContext.Current.CancellationToken);
        updatedSource.ShouldContain("SampleTestsHelpers.CreateTourId()", StringComparison.Ordinal);
        updatedSource.ShouldNotContain("private static int CreateTourId()", StringComparison.Ordinal);
        helperSource.ShouldContain("internal static class SampleTestsHelpers", StringComparison.Ordinal);
        helperSource.ShouldContain("internal static int CreateTourId()", StringComparison.Ordinal);
        (string.IsNullOrWhiteSpace(error.ToString())).ShouldBeTrue(error.ToString());
    }

    [Fact]
    public async Task Run_returns_zero_when_project_has_no_matching_diagnostics()
    {
        // Arrange
        var projectPath = Path.Combine(projectDirectory.Path, "Sample.Tests.csproj");
        var sourcePath = Path.Combine(projectDirectory.Path, "SampleTests.cs");
        await File.WriteAllTextAsync(projectPath, CodeFixRunnerTestProject.ProjectFile, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(sourcePath, CodeFixRunnerTestProject.CleanSourceFile, TestContext.Current.CancellationToken);

        var options = new CodeFixRunnerOptions(projectPath, "SKTEST006");
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var fixedCount = await CodeFixRunEngine.Run(options, error);
        var updatedSource = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);

        // Assert
        (fixedCount).ShouldBe(0);
        (updatedSource).ShouldContain("public void Execute()", StringComparison.Ordinal);
        (string.IsNullOrWhiteSpace(error.ToString())).ShouldBeTrue(error.ToString());
    }

    [Fact]
    public async Task Run_reports_unsupported_diagnostics_once_before_applying_supported_fixes()
    {
        // Arrange
        var projectPath = Path.Combine(projectDirectory.Path, "Sample.Tests.csproj");
        var sourcePath = Path.Combine(projectDirectory.Path, "SampleTests.cs");
        await File.WriteAllTextAsync(projectPath, CodeFixRunnerTestProject.ProjectFile, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(sourcePath, CodeFixRunnerTestProject.SupportedAndUnsupportedSourceFile, TestContext.Current.CancellationToken);

        var options = new CodeFixRunnerOptions(projectPath, "SKTEST006");
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var fixedCount = await CodeFixRunEngine.Run(options, error);

        // Assert
        (fixedCount).ShouldBe(1);
        var messages = error.ToString();
        (messages).ShouldContain("No code fix available", StringComparison.Ordinal);
        (messages.Split("No code fix available", StringSplitOptions.None).Length - 1).ShouldBe(1);
    }

    [Fact]
    public async Task Run_rejects_non_project_or_solution_path()
    {
        // Arrange
        var options = new CodeFixRunnerOptions(Path.Combine(projectDirectory.Path, "sample.txt"), "SKTEST006");
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        // Assert
        var exception = await ((Func<Task>)(() => CodeFixRunEngine.Run(options, error))).ShouldThrow<ArgumentException>();

        (exception.Message).ShouldContain("Expected a .csproj, .sln, or .slnx path.", StringComparison.Ordinal);
    }

    public void Dispose()
    {
        projectDirectory.Dispose();
    }
}
