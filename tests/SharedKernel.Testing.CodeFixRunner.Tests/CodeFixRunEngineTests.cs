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

            var options = new CodeFixRunnerOptions(projectPath, "SKTEST006");
            var error = new StringWriter(CultureInfo.InvariantCulture);

            // Act
            var fixedCount = await CodeFixRunEngine.Run(options, error);
            var updatedSource = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);

            // Assert
            (fixedCount).ShouldBe(1);
            (updatedSource).ShouldContain("using SharedKernel.Testing.Assertions;", StringComparison.Ordinal);
            (updatedSource).ShouldContain("(true).ShouldBeTrue()", StringComparison.Ordinal);
            (updatedSource).ShouldNotContain("Xunit.Assert.True");
            (string.IsNullOrWhiteSpace(error.ToString())).ShouldBeTrue(error.ToString());
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

            var options = new CodeFixRunnerOptions(projectPath, "SKTEST006");
            var error = new StringWriter(CultureInfo.InvariantCulture);

            // Act
            var fixedCount = await CodeFixRunEngine.Run(options, error);
            var updatedSource = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);

            // Assert
            (fixedCount).ShouldBe(0);
            (updatedSource).ShouldContain("public void Execute()", StringComparison.Ordinal);
            (string.IsNullOrWhiteSpace(error.ToString())).ShouldBeTrue(error.ToString());
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
            ArgumentException? exception = null;

            try
            {
                await CodeFixRunEngine.Run(options, error);
            }
            catch (ArgumentException caught)
            {
                exception = caught;
            }

            var nonNullException = exception.ShouldNotBeNull();

            (nonNullException.Message).ShouldContain("Expected a .csproj, .sln, or .slnx path.", StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }
}
