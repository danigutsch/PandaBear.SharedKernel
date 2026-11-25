using System.Text.Json;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Exercises the real SDK's NuGet Audit and proposal paths using synthetic metadata.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class NuGetTrialAdvisoryCommandTests
{
    [Fact]
    public async Task Finds_the_direct_owner_of_a_baseline_advisory_in_a_standalone_file_app()
    {
        // Arrange
        await using var audit = new NuGetTrialAuditFeed(NuGetTrialRepository.Core, "(,2.0.0)");
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize(audit: audit, baselineFileAppAdvisory: true);
        const string patch = ".local/file-app-owner.patch";
        var path = Path.Combine(fixture.Root, patch);

        // Act
        var result = await fixture.RunAudit(patch);

        // Assert
        var reportText = await File.ReadAllTextAsync(path + ".json", TestContext.Current.CancellationToken);
        using var report = JsonDocument.Parse(reportText);
        result.ExitCode.ShouldBe(0, reportText);
        report.RootElement.GetProperty("status").GetString().ShouldBe("verified");
        var steps = report.RootElement.GetProperty("steps").EnumerateArray();
        var baseline = steps.ShouldHaveSingleItem(step => step.GetProperty("name").GetString() == "baselineFileAppLockedRestore");
        var advisory = baseline.GetProperty("advisories").EnumerateArray().ShouldHaveSingleItem();
        advisory.GetProperty("project").GetString().ShouldBe(NuGetTrialRepository.FileAppPath);
        var minimum = report.RootElement.GetProperty("minimum");
        minimum.GetProperty("status").GetString().ShouldBe("proven");
        var selected = minimum.GetProperty("selectedUpdates").EnumerateArray().ShouldHaveSingleItem();
        selected.GetString().ShouldBe(NuGetTrialRepository.Driver + "@2.0.0");

        var affected = report.RootElement.GetProperty("baselinePackages").EnumerateArray().ShouldHaveSingleItem();
        var targets = affected.GetProperty("targets").EnumerateArray().Select(target => target.GetString()).ToArray();
        targets.ShouldContain($"{NuGetTrialRepository.FileAppPath} [net11.0]: 1.0.0 (Transitive)");
        var requestingPaths = affected.GetProperty("dependencyPaths").GetString();
        requestingPaths.ShouldNotBeNull().ShouldContain(NuGetTrialRepository.FileAppPath, StringComparison.Ordinal);
        File.Exists(path).ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, false, "unresolvedAdvisories", 1)]
    [InlineData(false, false, "verified", 0)]
    [InlineData(true, true, "minimumUnproven", 1)]
    public async Task File_app_advisories_follow_the_selected_upgrade_mode(
        bool vulnerable, bool sourceUnavailable, string status, int exitCode)
    {
        // Arrange
        await using var audit = new NuGetTrialAuditFeed(NuGetTrialRepository.Core, "(,2.0.0)", fileAppAdvisory: true);
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize(audit: audit, fileAppAdvisory: true, fileAppSourceUnavailable: sourceUnavailable);
        const string patch = ".local/file-app-advisory.patch";
        var path = Path.Combine(fixture.Root, patch);

        // Act
        var result = vulnerable
            ? await fixture.RunAudit(patch)
            : await fixture.RunDependency("trial-upgrade",
                "--plan", patch, "--copy-local-source", ".local/packages", "--update", NuGetTrialRepository.Driver + "@2.0.0");

        // Assert
        var reportText = await File.ReadAllTextAsync(path + ".json", TestContext.Current.CancellationToken);
        using var report = JsonDocument.Parse(reportText);
        var candidate = report.RootElement.GetProperty("steps").EnumerateArray().ShouldHaveSingleItem(step =>
            step.GetProperty("name").GetString() == "candidateFileAppLockedRestore");
        candidate.GetProperty("exitCode").GetInt32().ShouldBe(0, "The file app keeps its warning policy.");
        var advisory = candidate.GetProperty("advisories").EnumerateArray().ShouldHaveSingleItem();
        advisory.GetProperty("advisory").GetString().ShouldBe(NuGetTrialAuditFeed.FileAppAdvisory);
        report.RootElement.GetProperty("status").GetString().ShouldBe(status);
        result.ExitCode.ShouldBe(exitCode);
        File.Exists(path).ShouldBe(!vulnerable);
        if (sourceUnavailable)
        {
            report.RootElement.GetProperty("minimum").GetProperty("status").GetString().ShouldBe("candidateUnavailable");
        }
    }

    [Theory]
    [InlineData(NuGetTrialRepository.Driver, "(,2.0.0)", true, "verified")]
    [InlineData(NuGetTrialRepository.Core, "(,2.0.0)", true, "verified")]
    [InlineData(NuGetTrialRepository.Driver, "(,99.0.0)", true, "unresolvedAdvisories")]
    [InlineData(NuGetTrialRepository.Driver, "(,2.0.0)", false, "auditUnavailable")]
    public async Task Audit_trial_rechecks_the_candidate_under_the_original_warning_policy(
        string package, string range, bool auditAvailable, string expectedStatus)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(package);
        await using var audit = new NuGetTrialAuditFeed(package, range, auditAvailable);
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize(audit: audit);
        var config = Path.Combine(fixture.Root, "NuGet.Config");
        var props = Path.Combine(fixture.Root, "Directory.Packages.props");
        var originalConfig = await File.ReadAllTextAsync(config, TestContext.Current.CancellationToken);
        var originalProps = await File.ReadAllTextAsync(props, TestContext.Current.CancellationToken);
        const string patch = ".local/advisory.patch";

        // Act
        var result = await fixture.RunAudit(patch);

        // Assert
        var verified = expectedStatus == "verified";
        result.ExitCode.ShouldBe(verified ? 0 : 1);
        var reportText = await File.ReadAllTextAsync(Path.Combine(fixture.Root, patch + ".json"), TestContext.Current.CancellationToken);
        using var report = JsonDocument.Parse(reportText);
        report.RootElement.GetProperty("status").GetString().ShouldBe(expectedStatus);
        var evidence = report.RootElement.GetProperty("steps").EnumerateArray().ToArray();
        var exists = File.Exists(Path.Combine(fixture.Root, patch));
        exists.ShouldBe(verified);
        var currentConfig = await File.ReadAllTextAsync(config, TestContext.Current.CancellationToken);
        var currentProps = await File.ReadAllTextAsync(props, TestContext.Current.CancellationToken);
        currentConfig.ShouldBe(originalConfig);
        currentProps.ShouldBe(originalProps);

        if (auditAvailable)
        {
            reportText.ShouldContain(NuGetTrialAuditFeed.Advisory, StringComparison.Ordinal);
            var actual = report.RootElement.GetProperty("baselinePackages").EnumerateArray().ShouldHaveSingleItem();
            actual.GetProperty("package").GetString().ShouldBe(package);
            var targets = actual.GetProperty("targets").GetArrayLength();
            targets.ShouldBe(2);
        }

        if (verified)
        {
            var minimum = report.RootElement.GetProperty("minimum");
            minimum.GetProperty("status").GetString().ShouldBe("proven");
            var selected = minimum.GetProperty("selectedUpdates").EnumerateArray().ShouldHaveSingleItem();
            selected.GetString().ShouldBe(NuGetTrialRepository.Driver + "@2.0.0");
            var applied = await fixture.Git("apply", "--check", patch);
            applied.ExitCode.ShouldBe(0);
            var finalRestore = evidence.ShouldHaveSingleItem(step => step.GetProperty("name").GetString() == "candidateLockedRestore");
            finalRestore.GetProperty("exitCode").GetInt32().ShouldBe(0);
            finalRestore.GetProperty("advisories").GetArrayLength().ShouldBe(0);
        }
    }
}
