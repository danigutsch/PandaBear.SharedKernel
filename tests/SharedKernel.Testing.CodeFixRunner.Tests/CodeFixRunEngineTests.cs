using System.Globalization;

namespace SharedKernel.Testing.CodeFixRunner.Tests;

public sealed class CodeFixRunEngineTests
{
    private readonly string projectDirectory = CodeFixRunnerTestProject.CreateTemporaryProject();

    [Fact]
    public async Task Run_applies_SKTEST006_code_fix_to_project_file()
    {
        // Arrange
        try
        {
            var projectPath = Path.Combine(projectDirectory, "Sample.Tests.csproj");
            var sourcePath = Path.Combine(projectDirectory, "SampleTests.cs");
            await File.WriteAllTextAsync(projectPath, CodeFixRunnerTestProject.ProjectFile, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(sourcePath, CodeFixRunnerTestProject.SourceFile, TestContext.Current.CancellationToken);
            await CodeFixRunnerTestProject.Restore(projectPath);

            var options = new CodeFixRunnerOptions(projectPath, "SKTEST006");
            using var error = new StringWriter(CultureInfo.InvariantCulture);

            // Act
            var fixedCount = await CodeFixRunEngine.Run(options, error);
            var updatedSource = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);

            // Assert
            TestAssert.Equal(3, fixedCount);
            TestAssert.Contains("using SharedKernel.Testing.Assertions;", updatedSource, StringComparison.Ordinal);
            TestAssert.Contains(
                "TestAssert.True(true)",
                updatedSource,
                StringComparison.Ordinal);
            TestAssert.Contains(
                "TestAssert.ExactlyOne(new[] { 1 })",
                updatedSource,
                StringComparison.Ordinal);
            TestAssert.Contains(
                "TestAssert.Equal(\"a\", \"A\", StringComparer.OrdinalIgnoreCase)",
                updatedSource,
                StringComparison.Ordinal);
            TestAssert.DoesNotContain("Xunit.Assert.True", updatedSource, StringComparison.Ordinal);
            TestAssert.True(string.IsNullOrWhiteSpace(error.ToString()), error.ToString());
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Run_returns_zero_when_project_has_no_matching_diagnostics()
    {
        // Arrange
        try
        {
            var projectPath = Path.Combine(projectDirectory, "Sample.Tests.csproj");
            var sourcePath = Path.Combine(projectDirectory, "SampleTests.cs");
            await File.WriteAllTextAsync(projectPath, CodeFixRunnerTestProject.ProjectFile, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(sourcePath, CodeFixRunnerTestProject.CleanSourceFile, TestContext.Current.CancellationToken);
            await CodeFixRunnerTestProject.Restore(projectPath);

            var options = new CodeFixRunnerOptions(projectPath, "SKTEST006");
            using var error = new StringWriter(CultureInfo.InvariantCulture);

            // Act
            var fixedCount = await CodeFixRunEngine.Run(options, error);
            var updatedSource = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);

            // Assert
            TestAssert.Equal(0, fixedCount);
            TestAssert.Contains("public void Execute()", updatedSource, StringComparison.Ordinal);
            TestAssert.True(string.IsNullOrWhiteSpace(error.ToString()), error.ToString());
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Run_skips_diagnostic_when_no_code_fix_is_available()
    {
        // Arrange
        try
        {
            var projectPath = Path.Combine(projectDirectory, "Sample.Tests.csproj");
            var sourcePath = Path.Combine(projectDirectory, "SampleTests.cs");
            await File.WriteAllTextAsync(projectPath, CodeFixRunnerTestProject.ProjectFile, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(sourcePath, CodeFixRunnerTestProject.UnsupportedAssertSourceFile, TestContext.Current.CancellationToken);
            await CodeFixRunnerTestProject.Restore(projectPath);

            var options = new CodeFixRunnerOptions(projectPath, "SKTEST006");
            using var error = new StringWriter(CultureInfo.InvariantCulture);

            // Act
            var fixedCount = await CodeFixRunEngine.Run(options, error);
            var updatedSource = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);

            // Assert
            TestAssert.Equal(0, fixedCount);
            TestAssert.Contains("Xunit.Assert.Multiple", updatedSource, StringComparison.Ordinal);
            TestAssert.Contains("No code fix available", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Run_rejects_non_project_or_solution_path()
    {
        try
        {
            var options = new CodeFixRunnerOptions(Path.Combine(projectDirectory, "sample.txt"), "SKTEST006");
            using var error = new StringWriter(CultureInfo.InvariantCulture);

            var exception = await TestAssert.Throws<ArgumentException>(() => CodeFixRunEngine.Run(options, error));

            TestAssert.Contains("Expected a .csproj, .sln, or .slnx path.", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }
}
