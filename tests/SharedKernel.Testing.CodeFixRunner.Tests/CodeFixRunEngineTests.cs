using System.Globalization;
using SharedKernel.Testing.Assertions;

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
            TestAssert.Contains(
                "global::SharedKernel.Testing.Assertions.TestAssert.True(true)",
                updatedSource,
                StringComparison.Ordinal);
            TestAssert.Contains(
                "global::SharedKernel.Testing.Assertions.TestAssert.ExactlyOne(new[] { 1 })",
                updatedSource,
                StringComparison.Ordinal);
            TestAssert.Contains(
                "global::SharedKernel.Testing.Assertions.TestAssert.Equal(\"a\", \"A\", StringComparer.OrdinalIgnoreCase)",
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
}
