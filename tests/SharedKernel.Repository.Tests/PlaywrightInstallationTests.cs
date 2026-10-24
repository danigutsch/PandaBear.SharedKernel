namespace PandaBear.Repository.Tests;

public sealed class PlaywrightInstallationTests : IDisposable
{
    private const string Project = "tests/Core.Tests/Core.Tests.csproj";

    private readonly RepositoryTestScope scope = new();

    [Theory]
    [InlineData("bin\\Debug\\net10.0\\")]
    [InlineData("bin/Debug/net10.0/")]
    [InlineData("bin\\Debug/net10.0/")]
    public async Task The_installer_is_found_through_the_evaluated_output_path_with_either_separator(string outputPath)
    {
        // Arrange
        scope.WriteFile("tests/Core.Tests/bin/Debug/net10.0/playwright.ps1", "# installer");
        var processes = new ProcessLog((fileName, _, _) => Task.FromResult(fileName == "dotnet" ? outputPath + Environment.NewLine : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root,
            ["install-playwright", "--project", Project, "--browser", "chromium", "--browser", "firefox", "--with-dependencies"],
            processes.Run,
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Commands.ShouldBe(
        [
            "dotnet msbuild " + Path.GetFullPath(Path.Combine(scope.Root, Project)) + " -getProperty:OutputPath",
            "pwsh " + Path.Combine(scope.Root, "tests", "Core.Tests", "bin", "Debug", "net10.0", "playwright.ps1") + " install --with-deps chromium firefox",
        ]);
        processes.Calls.ShouldAllSatisfy(call => call.WorkingDirectory.ShouldBe(scope.Root));
    }

    [Fact]
    public async Task An_absolute_output_path_and_a_configuration_are_honored_without_dependencies()
    {
        // Arrange
        var output = Path.Combine(scope.Root, "artifacts", "bin", "Core.Tests", "release");
        scope.WriteFile("artifacts/bin/Core.Tests/release/playwright.ps1", "# installer");
        var processes = new ProcessLog((fileName, _, _) => Task.FromResult(fileName == "dotnet" ? output : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["install-playwright", "--project", Project, "--configuration", "Release"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Commands.ShouldBe(
        [
            "dotnet msbuild " + Path.GetFullPath(Path.Combine(scope.Root, Project)) + " -getProperty:OutputPath -p:Configuration=Release",
            "pwsh " + Path.Combine(output, "playwright.ps1") + " install",
        ]);
    }

    [Fact]
    public async Task A_project_without_a_built_installer_fails_before_starting_powershell()
    {
        // Arrange
        var processes = new ProcessLog(static (fileName, _, _) => Task.FromResult(fileName == "dotnet" ? "bin/Debug/net10.0/" : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["install-playwright", "--project", Project], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("playwright.ps1 is not in the build output of " + Project + "; build it first.", StringComparison.Ordinal);
        processes.Calls.ShouldAllSatisfy(static call => call.FileName.ShouldBe("dotnet"));
    }

    [Theory]
    [InlineData("--browser", "--with-deps")]
    [InlineData("--browser", "Chromium")]
    [InlineData("--configuration", "")]
    public async Task Option_values_that_could_change_the_installer_command_fail_to_parse(string option, string value)
    {
        // Arrange
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["install-playwright", "--project", Project, option, value], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        processes.Calls.ShouldBeEmpty();
    }

    /// <inheritdoc />
    public void Dispose() => scope.Dispose();
}
