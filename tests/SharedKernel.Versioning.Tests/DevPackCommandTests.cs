using PandaBear.Versioning.Tool;

namespace PandaBear.Versioning.Tests;

/// <summary>Tests how the <c>pack-dev</c> command turns its options into pack invocations.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class DevPackCommandTests
{
    private const string Version = "0.1.0-dev.local";

    /// <summary>A relative repository root yields an absolute working directory and project path that exist.</summary>
    [Fact]
    public static void A_relative_repo_root_is_resolved_once_for_the_working_directory_and_project_paths()
    {
        // Arrange
        using var repository = new DevPackRepository();
        var project = repository.AddProject("SharedKernel.Results", "PandaBear.SharedKernel.Results");
        var relativeRoot = Path.GetRelativePath(Directory.GetCurrentDirectory(), repository.Root);
        var options = new DevPackOptions(repository.Feed, Version, relativeRoot, []);

        // Act
        var invocation = DevPackCommand.CreateInvocations(options, repository.Feed).ShouldHaveSingleItem();

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
        var line = DevPackCommand.FormatKept(step);

        // Assert
        line.ShouldBe("Kept   PandaBear.SharedKernel.Results (stale: sources changed after it was packed; name it to repack)");
    }

    /// <summary>A current kept step keeps the plain report line.</summary>
    [Fact]
    public static void A_current_kept_step_is_reported_without_a_warning()
    {
        // Arrange
        var step = new DevPackStep("SharedKernel.Results", "/repo/Results.csproj", "PandaBear.SharedKernel.Results", Rebuild: false);

        // Act
        var line = DevPackCommand.FormatKept(step);

        // Assert
        line.ShouldBe("Kept   PandaBear.SharedKernel.Results");
    }
}
