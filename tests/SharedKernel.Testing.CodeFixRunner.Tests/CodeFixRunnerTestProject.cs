using System.Diagnostics;
using System.Globalization;
using SharedKernel.Testing.Assertions;

namespace SharedKernel.Testing.CodeFixRunner.Tests;

internal static class CodeFixRunnerTestProject
{
    public const string ProjectFile = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="xunit.v3.assert" Version="3.2.2" />
          </ItemGroup>
        </Project>
        """;

    public const string SourceFile = """
        namespace Sample.Tests;

        public sealed class SampleTests
        {
            public void UsesXunitAssert()
            {
                Xunit.Assert.True(true);
                _ = Xunit.Assert.Single(new[] { 1 });
                Xunit.Assert.Equal("a", "A", ignoreCase: true);
            }
        }
        """;

    public const string CleanSourceFile = """
        namespace Sample.Tests;

        public sealed class SampleTests
        {
            public void Execute()
            {
            }
        }
        """;

    public const string UnsupportedAssertSourceFile = """
        namespace Sample.Tests;

        public sealed class SampleTests
        {
            public void Execute()
            {
                Xunit.Assert.Multiple(() => { });
            }
        }
        """;

    public static string CreateTemporaryProject()
    {
        var projectDirectory = Path.Combine(Path.GetTempPath(), "sk-codefix-runner-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(projectDirectory);
        return projectDirectory;
    }

    public static async Task Restore(string projectPath)
    {
        var startInfo = new ProcessStartInfo("dotnet", $"restore \"{projectPath}\"")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet restore.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        TestAssert.True(
            process.ExitCode == 0,
            $"dotnet restore failed with exit code {process.ExitCode}.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
    }
}
