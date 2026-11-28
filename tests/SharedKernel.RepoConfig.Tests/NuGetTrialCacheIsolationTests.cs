using System.Text.Json;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Exercises the trial fixture with a conflicting package in a caller-owned cache.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class NuGetTrialCacheIsolationTests
{
    [Fact]
    public async Task Trial_uses_the_fixture_cache_when_the_caller_sets_a_package_cache()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var cache = await NuGetTrialCallerCache.Seed(directory.Path, ct);
        var before = NuGetTrialCallerCache.Inventory(cache);
        var test = typeof(NuGetTrialCacheIsolationTests).FullName + "." + nameof(Resolves_the_fixture_candidate_in_the_child_probe);
        var environment = new Dictionary<string, string?>
        {
            [NuGetTrialCallerCache.ProbeVariable] = cache,
            ["NUGET_PACKAGES"] = cache,
        };

        // Act
        var result = await TestProcess.Run("dotnet", AppContext.BaseDirectory,
            [typeof(NuGetTrialCacheIsolationTests).Assembly.Location, "--filter-method", test, "--explicit", "only"], ct, environment);
        var after = NuGetTrialCallerCache.Inventory(cache);

        // Assert
        before.ShouldNotBeEmpty();
        result.ExitCode.ShouldBe(0, result.StandardOutput + result.StandardError);
        after.ShouldBe(before);
    }

    [Fact(Explicit = true)]
    public async Task Resolves_the_fixture_candidate_in_the_child_probe()
    {
        // Arrange
        var cache = Environment.GetEnvironmentVariable(NuGetTrialCallerCache.ProbeVariable);
        if (cache is null)
        {
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var audit = new NuGetTrialAuditFeed(NuGetTrialRepository.Core, "(,2.0.0)");
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize(audit: audit);
        const string patch = ".local/cache-isolation.patch";

        // Act
        var result = await fixture.RunAudit(patch);
        var reportText = await File.ReadAllTextAsync(Path.Combine(fixture.Root, patch + ".json"), ct);

        // Assert
        Environment.GetEnvironmentVariable("NUGET_PACKAGES").ShouldBe(cache);
        result.ExitCode.ShouldBe(0, result.StandardError + reportText);
        using var report = JsonDocument.Parse(reportText);
        var graph = report.RootElement.GetProperty("graph").GetProperty("projects").EnumerateArray();
        var project = graph.ShouldHaveSingleItem(item => item.GetProperty("project").GetString() == "src/locked/Locked.csproj");
        var core = project.GetProperty("changes").EnumerateArray()
            .ShouldHaveSingleItem(item => item.GetProperty("package").GetString() == NuGetTrialRepository.Core);
        var after = core.GetProperty("after");
        after.ValueKind.ShouldBe(JsonValueKind.Object, "The fixture's candidate must retain the Core dependency.");
        after.GetProperty("version").GetString().ShouldBe("2.0.0");
    }
}
