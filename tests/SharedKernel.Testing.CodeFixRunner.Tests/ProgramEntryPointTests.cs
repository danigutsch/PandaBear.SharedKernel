using System.Globalization;

namespace SharedKernel.Testing.CodeFixRunner.Tests;

public sealed class ProgramEntryPointTests
{
    [Fact]
    public async Task Run_writes_fixed_count_and_returns_zero_when_arguments_are_valid()
    {
        using var projectDirectory = TemporaryProjectDirectory.Create();

        var projectPath = Path.Combine(projectDirectory.Path, "Sample.Tests.csproj");
        var sourcePath = Path.Combine(projectDirectory.Path, "SampleTests.cs");
        await File.WriteAllTextAsync(projectPath, CodeFixRunnerTestProject.ProjectFile, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(sourcePath, CodeFixRunnerTestProject.SourceFile, TestContext.Current.CancellationToken);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        var exitCode = await ProgramEntryPoint.Run(["--diagnostic", "SKTEST006", projectPath], output, error);

        (exitCode).ShouldBe(0);
        (output.ToString()).ShouldContain("Fixed 1 SKTEST006 diagnostic(s).", StringComparison.Ordinal);
        (string.IsNullOrWhiteSpace(error.ToString())).ShouldBeTrue(error.ToString());
    }

    [Fact]
    public async Task Run_writes_usage_and_returns_two_when_arguments_are_invalid()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        var exitCode = await ProgramEntryPoint.Run([], output, error);

        (exitCode).ShouldBe(2);
        (error.ToString()).ShouldContain(CodeFixRunnerOptions.Usage, StringComparison.Ordinal);
        (string.IsNullOrWhiteSpace(output.ToString())).ShouldBeTrue(output.ToString());
    }
}
