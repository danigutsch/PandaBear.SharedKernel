using System.Text.Json;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Exercises dependency evidence commands against a real restored solution.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class NuGetDependencyEvidenceCommandTests
{
    [Fact]
    public async Task Inspect_pin_reports_requesting_paths_from_locked_and_lock_exempt_projects()
    {
        // Arrange
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize();
        await fixture.RestoreLocked();

        // Act
        var result = await fixture.RunDependency("inspect-pin", "--package", NuGetTrialRepository.Core);

        // Assert
        result.ExitCode.ShouldBe(0, result.StandardError);
        result.StandardOutput.ShouldContain("src/locked/Locked.csproj [net11.0]: 2.0.0 (CentralTransitive, locked)", StringComparison.Ordinal);
        result.StandardOutput.ShouldContain("src/exempt/Exempt.csproj [net11.0]: 2.0.0 (CentralTransitive, lock-exempt)", StringComparison.Ordinal);
        result.StandardOutput.ShouldContain(NuGetTrialRepository.Driver, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_pin_reports_that_no_resolved_target_selects_an_absent_package()
    {
        // Arrange
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize();
        await fixture.RestoreLocked();

        // Act
        var result = await fixture.RunDependency("inspect-pin", "--package", "Missing.Package");

        // Assert
        result.ExitCode.ShouldBe(0, result.StandardError);
        result.StandardOutput.ShouldContain("No restored target selects this package.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_pin_names_the_evaluated_file_app_owner_and_its_imported_version_file()
    {
        // Arrange
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize(fileCore: true);
        await fixture.RestoreLocked();

        // Act
        var result = await TestProcess.Run("dotnet", fixture.Root,
            [typeof(RepoConfigCommandLine).Assembly.Location, "dependency", "inspect-pin", "--root", fixture.Root, "--package", NuGetTrialRepository.Core],
            TestContext.Current.CancellationToken,
            new Dictionary<string, string?> { ["DOTNET_CLI_CONTEXT_VERBOSE"] = "true" });

        // Assert
        result.ExitCode.ShouldBe(0, result.StandardError);
        result.StandardOutput.ShouldContain(NuGetTrialRepository.FileAppPath + " [net11.0]", StringComparison.Ordinal);
        result.StandardOutput.ShouldContain("direct file-based declaration from " + NuGetTrialRepository.VersionsPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compare_reports_missing_file_based_restore_evidence()
    {
        // Arrange
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize(fileCore: true);
        await fixture.RestoreLocked();
        const string path = ".local/file-app-comparison.json";

        // Act
        var result = await fixture.RunDependency("compare-restore", "--candidate", ".", "--output", path);

        // Assert
        result.ExitCode.ShouldBe(1, result.StandardOutput + result.StandardError);
        var json = await File.ReadAllTextAsync(Path.Combine(fixture.Root, path), TestContext.Current.CancellationToken);
        using var report = JsonDocument.Parse(json);
        var app = report.RootElement.GetProperty("projects").EnumerateArray()
            .ShouldHaveSingleItem(project => project.GetProperty("project").GetString() == NuGetTrialRepository.FileAppPath);
        app.GetProperty("baselineUnavailable").GetString().ShouldContain("file-based", StringComparison.Ordinal);
        app.GetProperty("candidateUnavailable").GetString().ShouldContain("file-based", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compare_restore_outputs_deterministic_JSON_for_complete_restore_evidence()
    {
        // Arrange
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize();
        await fixture.RestoreLocked();
        const string firstPath = ".local/first.json";
        const string secondPath = ".local/second.json";

        // Act
        var first = await fixture.RunDependency("compare-restore", "--candidate", ".", "--output", firstPath);
        var second = await fixture.RunDependency("compare-restore", "--candidate", ".", "--output", secondPath);

        // Assert
        first.ExitCode.ShouldBe(0, first.StandardError);
        second.ExitCode.ShouldBe(0, second.StandardError);
        first.StandardOutput.ShouldContain("0 package changes across 2 projects.", StringComparison.Ordinal);
        var firstJson = await File.ReadAllTextAsync(Path.Combine(fixture.Root, firstPath), TestContext.Current.CancellationToken);
        var secondJson = await File.ReadAllTextAsync(Path.Combine(fixture.Root, secondPath), TestContext.Current.CancellationToken);
        secondJson.ShouldBe(firstJson);
        using var report = JsonDocument.Parse(firstJson);
        var projects = report.RootElement.GetProperty("projects").EnumerateArray();
        var exempt = projects.ShouldHaveSingleItem(project => project.GetProperty("baselineLockExempt").GetBoolean());
        exempt.GetProperty("candidateLockExempt").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Evidence_commands_report_a_lock_exempt_project_whose_assets_are_missing()
    {
        // Arrange
        using var baseline = new NuGetTrialRepository();
        await baseline.Initialize();
        await baseline.RestoreLocked();
        using var candidate = new NuGetTrialRepository();
        await candidate.Initialize();
        await candidate.RestoreLocked();
        File.Delete(Path.Combine(candidate.Root, "src/exempt/obj/project.assets.json"));
        const string reportPath = ".local/incomplete.json";

        // Act
        var inspection = await candidate.RunDependency("inspect-pin", "--package", NuGetTrialRepository.Core);
        var comparison = await baseline.RunDependency("compare-restore", "--candidate", candidate.Root, "--output", reportPath);

        // Assert
        inspection.ExitCode.ShouldBe(1);
        inspection.StandardError.ShouldContain("src/exempt/Exempt.csproj: Restore assets or cache are missing.", StringComparison.Ordinal);
        comparison.ExitCode.ShouldBe(1);
        comparison.StandardError.ShouldContain("Restore evidence is unavailable for 1 projects.", StringComparison.Ordinal);
        var json = await File.ReadAllTextAsync(Path.Combine(baseline.Root, reportPath), TestContext.Current.CancellationToken);
        using var report = JsonDocument.Parse(json);
        var unavailable = report.RootElement.GetProperty("projects").EnumerateArray()
            .ShouldHaveSingleItem(project => project.GetProperty("candidateUnavailable").ValueKind == JsonValueKind.String);
        unavailable.GetProperty("candidateLockExempt").GetBoolean().ShouldBeTrue();
        unavailable.GetProperty("baselineUnavailable").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
