using PandaBear.Processes;

namespace PandaBear.Versioning.Tests;

/// <summary>Tests how the dev pack turns its options into pack invocations.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class DevPackTests
{
    private const string Version = "0.1.0-dev.local";
    private static readonly ExecutableMap UsrBin = new(static command => "/usr/bin/" + command);

    /// <summary>A relative repository root yields an absolute working directory and project path that exist.</summary>
    [Fact]
    public static void A_relative_repo_root_is_resolved_once_for_the_working_directory_and_project_paths()
    {
        // Arrange
        using var repository = new DevPackRepository();
        var project = repository.AddProject("SharedKernel.Results", "PandaBear.SharedKernel.Results");
        var relativeRoot = Path.GetRelativePath(Directory.GetCurrentDirectory(), repository.Root);
        var options = new DevPackOptions(repository.Feed, Version, relativeRoot, [], Directory.GetCurrentDirectory(), NuGetPackages: null);

        // Act
        var invocation = DevPack.CreateInvocations(options, repository.Feed).ShouldHaveSingleItem();

        // Assert
        invocation.WorkingDirectory.ShouldBe(repository.Root);
        invocation.Arguments[1].ShouldBe(project);
        Path.IsPathRooted(invocation.Arguments[1]).ShouldBeTrue();
    }

    /// <summary>A kept step with stale sources is reported with a warning that names the remedy.</summary>
    [Fact]
    public static void A_stale_kept_step_is_reported_with_a_warning()
    {
        // Arrange
        var step = new DevPackStep("SharedKernel.Results", "/repo/Results.csproj", "PandaBear.SharedKernel.Results", Rebuild: false, Stale: true);

        // Act
        var line = DevPack.FormatKept(step);

        // Assert
        line.ShouldBe("Kept   PandaBear.SharedKernel.Results (stale: sources changed after it was packed; name it to repack)");
    }

    /// <summary>
    /// Without a configured cache, a run asks <c>dotnet</c> for the global packages folder, packs the named project into the feed,
    /// evicts the stale cached copy of the package and reports both.
    /// </summary>
    /// <returns>A task that completes when the run has been checked.</returns>
    [Fact]
    public static async Task A_run_packs_the_named_project_into_the_feed_and_evicts_its_cached_copy()
    {
        // Arrange
        using var repository = new DevPackRepository();
        repository.AddProject("SharedKernel.Results", "PandaBear.SharedKernel.Results");
        var cache = Path.Combine(repository.Root, "nuget-packages");
        var cached = Directory.CreateDirectory(Path.Combine(cache, "pandabear.sharedkernel.results", Version)).FullName;
        var runner = ScriptedProcessRunner.Answering(run => new ProcessOutput(
            0,
            run.Arguments.SequenceEqual(["nuget", "locals", "global-packages", "--list"]) ? "global-packages: " + cache + "\n" : string.Empty));
        var options = new DevPackOptions(repository.Feed, Version, repository.Root, ["SharedKernel.Results"], repository.Root, NuGetPackages: null);
        using var output = new InvariantStringWriter();

        // Act
        var feed = await DevPack.Run(new ExternalCommands(runner, UsrBin), options, output, TestContext.Current.CancellationToken);

        // Assert
        feed.ShouldBe(repository.Feed);
        runner.Runs.Select(static run => run.Arguments[0]).ShouldBe(["nuget", "pack"]);
        Directory.Exists(cached).ShouldBeFalse();
        output.ToString().ShouldContain("Packed PandaBear.SharedKernel.Results " + Version + " (evicted from " + cache + ")", StringComparison.Ordinal);
    }

    /// <summary>A current kept step keeps the plain report line.</summary>
    [Fact]
    public static void A_current_kept_step_is_reported_without_a_warning()
    {
        // Arrange
        var step = new DevPackStep("SharedKernel.Results", "/repo/Results.csproj", "PandaBear.SharedKernel.Results", Rebuild: false);

        // Act
        var line = DevPack.FormatKept(step);

        // Assert
        line.ShouldBe("Kept   PandaBear.SharedKernel.Results");
    }
}
