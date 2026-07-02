using System.Globalization;
using SharedKernel.Testing.Assertions;

namespace SharedKernel.Testing.CodeFixRunner.Tests;

public sealed class ProgramEntryPointTests
{
    [Fact]
    public async Task Run_writes_fixed_count_and_returns_zero_when_arguments_are_valid()
    {
        var projectDirectory = CodeFixRunnerTestProject.CreateTemporaryProject();

        try
        {
            var projectPath = Path.Combine(projectDirectory, "Sample.Tests.csproj");
            var sourcePath = Path.Combine(projectDirectory, "SampleTests.cs");
            await File.WriteAllTextAsync(projectPath, CodeFixRunnerTestProject.ProjectFile, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(sourcePath, CodeFixRunnerTestProject.SourceFile, TestContext.Current.CancellationToken);
            await CodeFixRunnerTestProject.Restore(projectPath);
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            using var error = new StringWriter(CultureInfo.InvariantCulture);

            var exitCode = await ProgramEntryPoint.Run(["--diagnostic", "SKTEST006", projectPath], output, error);

            TestAssert.Equal(0, exitCode);
            TestAssert.Contains("Fixed 3 SKTEST006 diagnostic(s).", output.ToString(), StringComparison.Ordinal);
            TestAssert.True(string.IsNullOrWhiteSpace(error.ToString()), error.ToString());
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Run_writes_usage_and_returns_two_when_arguments_are_invalid()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        var exitCode = await ProgramEntryPoint.Run([], output, error);

        TestAssert.Equal(2, exitCode);
        TestAssert.Contains(CodeFixRunnerOptions.Usage, error.ToString(), StringComparison.Ordinal);
        TestAssert.True(string.IsNullOrWhiteSpace(output.ToString()), output.ToString());
    }
}
