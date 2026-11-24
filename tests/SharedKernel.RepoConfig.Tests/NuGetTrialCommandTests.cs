using System.Text.Json;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Exercises dependency trials with the repository-selected SDK and a mapped feed.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class NuGetTrialCommandTests
{
    [Fact]
    public async Task Trial_removes_an_unused_pin_with_empty_dependency_path_evidence()
    {
        // Arrange
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize();
        var ct = TestContext.Current.CancellationToken;
        const string pin = "PandaBear.NuGetTrialFixture.Unused";
        const string patch = ".local/remove-unused.patch";
        var props = Path.Combine(fixture.Root, NuGetTrialRepository.VersionsPath);
        var original = await File.ReadAllTextAsync(props, ct);
        var withUnusedPin = original.Replace("</ItemGroup>",
            $"  <PackageVersion Include='{pin}' Version='1.0.0'/>\r\n  </ItemGroup>", StringComparison.Ordinal);
        await File.WriteAllTextAsync(props, withUnusedPin, ct);

        // Act
        var result = await fixture.RunDependency("trial-upgrade", "--props", NuGetTrialRepository.VersionsPath,
            "--remove-pin", pin, "--plan", patch, "--copy-local-source", ".local/packages");

        // Assert
        var path = Path.Combine(fixture.Root, patch);
        var reportText = await File.ReadAllTextAsync(path + ".json", ct);
        result.ExitCode.ShouldBe(0, reportText);
        using var report = JsonDocument.Parse(reportText);
        var baseline = report.RootElement.GetProperty("baselinePackages").EnumerateArray().ShouldHaveSingleItem();
        baseline.GetProperty("package").GetString().ShouldBe(pin);
        baseline.GetProperty("targets").EnumerateArray().ShouldBeEmpty();
        baseline.GetProperty("dependencyPaths").ValueKind.ShouldBe(JsonValueKind.Null);
        var changes = report.RootElement.GetProperty("graph").GetProperty("projects").EnumerateArray()
            .SelectMany(project => project.GetProperty("changes").EnumerateArray()).ToArray();
        changes.ShouldBeEmpty();
        var originalCheckout = await File.ReadAllTextAsync(props, ct);
        originalCheckout.ShouldBe(withUnusedPin);
        File.Exists(path).ShouldBeTrue();
    }

    [Fact]
    public async Task Trial_compares_coordinated_changes_and_applies_idempotently_to_imported_props()
    {
        // Arrange
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize();
        var props = Path.Combine(fixture.Root, NuGetTrialRepository.VersionsPath);
        var lockFile = Path.Combine(fixture.Root, NuGetTrialRepository.LockedPath);
        var original = await File.ReadAllTextAsync(props, TestContext.Current.CancellationToken);
        var originalLock = await File.ReadAllTextAsync(lockFile, TestContext.Current.CancellationToken);
        const string patch = ".local/review.patch";
        string[] updates = ["--update", NuGetTrialRepository.Driver + "@2.0.0", "--update", NuGetTrialRepository.Partner + "@2.0.0"];

        // Act
        var result = await fixture.RunTrial(patch, [.. updates, "--build"]);

        // Assert
        if (result.ExitCode != 0)
        {
            var failedReport = Path.Combine(fixture.Root, patch + ".json");
            var details = File.Exists(failedReport) ? await File.ReadAllTextAsync(failedReport, TestContext.Current.CancellationToken) : string.Empty;
            throw new InvalidOperationException(result.StandardOutput + result.StandardError + details);
        }

        var sourceProps = await File.ReadAllTextAsync(props, TestContext.Current.CancellationToken);
        var sourceLock = await File.ReadAllTextAsync(lockFile, TestContext.Current.CancellationToken);
        sourceProps.ShouldBe(original);
        sourceLock.ShouldBe(originalLock);
        using var graph = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(fixture.Root, patch + ".json"), TestContext.Current.CancellationToken));
        var projects = graph.RootElement.GetProperty("graph").GetProperty("projects");
        projects.GetArrayLength().ShouldBe(2);
        projects.EnumerateArray().Count(project => project.GetProperty("candidateLockExempt").GetBoolean()).ShouldBe(1);
        var steps = graph.RootElement.GetProperty("steps");
        var build = steps.EnumerateArray().ShouldHaveSingleItem(step => step.GetProperty("name").GetString() == "build");
        build.GetProperty("exitCode").GetInt32().ShouldBe(0);

        var check = await fixture.Git("apply", "--check", patch);
        check.ExitCode.ShouldBe(0);
        var applied = await fixture.Git("apply", patch);
        applied.ExitCode.ShouldBe(0);
        var after = await File.ReadAllTextAsync(props, TestContext.Current.CancellationToken);
        after.ShouldContain("<!-- Keep this comment and the single quotes. -->\r\n", StringComparison.Ordinal);
        after.ShouldContain("Version='2.0.0'/>\r\n", StringComparison.Ordinal);
        after.ShouldNotContain(NuGetTrialRepository.Core, StringComparison.Ordinal);
        File.Exists(Path.Combine(fixture.Root, "src/exempt/packages.lock.json")).ShouldBeFalse();

        var second = await fixture.RunTrial(".local/second.patch", updates);
        second.ExitCode.ShouldBe(0);
        second.StandardOutput.ShouldContain("No changes", StringComparison.Ordinal);
        File.Exists(Path.Combine(fixture.Root, ".local/second.patch")).ShouldBeFalse();
    }

    [Fact]
    public async Task Trial_proposes_a_direct_owner_and_coordinated_provider_from_mapped_sources()
    {
        // Arrange
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize();
        const string path = ".local/proposal.patch";
        string[] arguments = ["--propose", "--coordinate", NuGetTrialRepository.Partner];
        var props = Path.Combine(fixture.Root, NuGetTrialRepository.VersionsPath);
        var original = await File.ReadAllTextAsync(props, TestContext.Current.CancellationToken);

        // Act
        var result = await fixture.RunTrial(path, arguments);

        // Assert
        if (result.ExitCode != 0)
        {
            var failedReport = Path.Combine(fixture.Root, path + ".json");
            var details = File.Exists(failedReport) ? await File.ReadAllTextAsync(failedReport, TestContext.Current.CancellationToken) : string.Empty;
            throw new InvalidOperationException(result.StandardError + details);
        }

        var sourceProps = await File.ReadAllTextAsync(props, TestContext.Current.CancellationToken);
        sourceProps.ShouldBe(original);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(fixture.Root, path + ".json"), TestContext.Current.CancellationToken));
        var proposal = report.RootElement.GetProperty("steps").EnumerateArray()
            .ShouldHaveSingleItem(step => step.GetProperty("name").GetString() == "sdkPinOwnerUpdate");
        var owners = proposal.GetProperty("packages");
        owners.EnumerateArray().Select(owner => owner.GetString()).ShouldBe([NuGetTrialRepository.Driver, NuGetTrialRepository.Partner]);

        var check = await fixture.Git("apply", "--check", path);
        check.ExitCode.ShouldBe(0);
        var applied = await fixture.Git("apply", path);
        applied.ExitCode.ShouldBe(0);
        var after = await File.ReadAllTextAsync(props, TestContext.Current.CancellationToken);
        after.ShouldContain(NuGetTrialRepository.Driver + "' Version='2.0.0'/>\r\n", StringComparison.Ordinal);
        after.ShouldContain(NuGetTrialRepository.Partner + "' Version='2.0.0'/>\r\n", StringComparison.Ordinal);
        after.ShouldNotContain(NuGetTrialRepository.Core, StringComparison.Ordinal);
        File.Exists(Path.Combine(fixture.Root, "src/exempt/packages.lock.json")).ShouldBeFalse();

        var repeat = await fixture.RunTrial(".local/repeated-proposal.patch", arguments);
        repeat.ExitCode.ShouldBe(0);
        repeat.StandardOutput.ShouldContain("No changes", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trial_keeps_a_pin_whose_removal_lowers_the_dependency_floor()
    {
        // Arrange
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize();
        const string path = ".local/hold.patch";

        // Act
        var result = await fixture.RunTrial(path);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("lower or unconfirmed dependency floor", StringComparison.Ordinal);
        File.Exists(Path.Combine(fixture.Root, path)).ShouldBeFalse();
        var recorded = await File.ReadAllTextAsync(Path.Combine(fixture.Root, path + ".json"), TestContext.Current.CancellationToken);
        using var report = JsonDocument.Parse(recorded);
        report.RootElement.GetProperty("status").GetString().ShouldBe("pinRequired");
    }

    [Fact]
    public async Task Trial_preserves_a_central_declaration_used_by_direct_references()
    {
        // Arrange
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize(directCore: true);
        const string path = ".local/direct.patch";

        // Act
        var result = await fixture.RunTrial(path);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("Retain the direct declaration", StringComparison.Ordinal);
        result.StandardError.ShouldContain("src/locked/Locked.csproj (net11.0)", StringComparison.Ordinal);
        result.StandardError.ShouldContain("src/exempt/Exempt.csproj (net11.0)", StringComparison.Ordinal);
        File.Exists(Path.Combine(fixture.Root, path)).ShouldBeFalse();
    }

    [Fact]
    public async Task Trial_preserves_an_imported_central_declaration_used_by_a_file_based_app()
    {
        // Arrange
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize(fileCore: true);
        const string path = ".local/file-app-required.patch";
        string[] updates = ["--update", NuGetTrialRepository.Driver + "@2.0.0", "--update", NuGetTrialRepository.Partner + "@2.0.0"];
        var before = await fixture.Git("status", "--porcelain");

        // Act
        var result = await fixture.RunTrial(path, updates);

        // Assert
        result.ExitCode.ShouldBe(1, result.StandardOutput + result.StandardError);
        result.StandardError.ShouldContain("Retain the direct declaration", StringComparison.Ordinal);
        result.StandardError.ShouldContain(NuGetTrialRepository.FileAppPath + " (net11.0)", StringComparison.Ordinal);
        File.Exists(Path.Combine(fixture.Root, path)).ShouldBeFalse();
        var after = await fixture.Git("status", "--porcelain");
        after.StandardOutput.ShouldBe(before.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Trial_removes_a_redundant_pin_when_the_file_based_app_uses_its_own_central_declaration(bool trackedFileLock)
    {
        // Arrange
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize(fileCore: true, nestedFileVersions: true, trackedFileLock: trackedFileLock);
        const string path = ".local/scoped-file-app.patch";
        string[] updates = ["--update", NuGetTrialRepository.Driver + "@2.0.0", "--update", NuGetTrialRepository.Partner + "@2.0.0"];
        var nestedProps = Path.Combine(fixture.Root, "operations", "Directory.Packages.props");
        var ct = TestContext.Current.CancellationToken;
        var before = await File.ReadAllTextAsync(nestedProps, ct);

        // Act
        var result = await fixture.RunTrial(path, updates);

        // Assert
        result.ExitCode.ShouldBe(0, result.StandardError);
        var patch = await File.ReadAllTextAsync(Path.Combine(fixture.Root, path), ct);
        patch.ShouldContain("eng/Versions.props", StringComparison.Ordinal);
        patch.ShouldNotContain("operations/Directory.Packages.props", StringComparison.Ordinal);
        var after = await File.ReadAllTextAsync(nestedProps, ct);
        after.ShouldBe(before);
        var recorded = await File.ReadAllTextAsync(Path.Combine(fixture.Root, path + ".json"), ct);
        using var report = JsonDocument.Parse(recorded);
        var app = report.RootElement.GetProperty("graph").GetProperty("projects").EnumerateArray()
            .ShouldHaveSingleItem(project => project.GetProperty("project").GetString() == NuGetTrialRepository.FileAppPath);
        app.GetProperty("baselineUnavailable").ValueKind.ShouldBe(JsonValueKind.Null);
        app.GetProperty("candidateUnavailable").ValueKind.ShouldBe(JsonValueKind.Null);
        app.GetProperty("changes").GetArrayLength().ShouldBe(0);
        var usedScratch = report.RootElement.GetProperty("steps").EnumerateArray()
            .Any(step => step.GetProperty("name").GetString() == "baselineFileAppRestore");
        usedScratch.ShouldBe(!trackedFileLock);
        File.Exists(Path.Combine(fixture.Root, "operations", "packages.lock.json")).ShouldBe(trackedFileLock);
    }
}
