namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests that the Git change guard accepts only digest-bound changes under <c>roadmap/</c> and rejects unsafe paths, links, submodules, copies, and malformed status.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RoadmapGitChangeGuardTests
{
    /// <summary>Capturing an empty Git status reports no changes and no paths.</summary>
    [Fact]
    public void Empty_status_produces_an_idempotent_no_diff_result()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var changes = RoadmapGitChangeGuard.Capture(workspace.RootPath, []);

        // Assert
        changes.HasChanges.ShouldBeFalse();
        changes.Paths.ShouldBeEmpty();
    }

    /// <summary>An untracked <c>roadmap/items/RM-100.json</c> is captured as the only changed path, and rewriting its content with the same status produces a different digest.</summary>
    [Fact]
    public void Roadmap_file_changes_are_digest_bound()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        const string path = "roadmap/items/RM-100.json";
        workspace.WriteFile(path, "{\"id\":\"RM-100\"}");
        var status = RoadmapGitStatusTestData.Untracked(path);

        // Act
        var first = RoadmapGitChangeGuard.Capture(workspace.RootPath, status);
        workspace.WriteFile(path, "{\"id\":\"RM-101\"}");
        var second = RoadmapGitChangeGuard.Capture(workspace.RootPath, status);

        // Assert
        first.HasChanges.ShouldBeTrue();
        first.Paths.ShouldBe([path]);
        first.Digest.ShouldNotBe(second.Digest);
    }

    /// <summary>An untracked <c>tools/unsafe.json</c> makes capture throw that a change outside <c>roadmap/**</c> was detected.</summary>
    [Fact]
    public void Outside_roadmap_changes_are_rejected()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        const string path = "tools/unsafe.json";
        workspace.WriteFile(path, "{}");

        // Act
        Action action = () => RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Untracked(path));
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation detected a change outside roadmap/**.");
    }

    /// <summary>A rename from <c>tools/original.json</c> to a roadmap path makes capture throw the outside-roadmap error, because the original path is checked too.</summary>
    [Fact]
    public void Rename_validation_checks_both_paths()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        const string path = "roadmap/items/RM-100.json";
        workspace.WriteFile(path, "{}");

        // Act
        Action action = () => RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Rename(path, "tools/original.json"));
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation detected a change outside roadmap/**.");
    }

    /// <summary>A copy record between two roadmap item files makes capture throw that Git copy changes are rejected.</summary>
    [Fact]
    public void Git_copy_status_is_rejected_instead_of_deleting_the_source()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("roadmap/items/RM-101.json", "{}");

        // Act
        Action action = () => RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Copy("roadmap/items/RM-101.json", "roadmap/items/RM-100.json"));
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation rejects Git copy changes.");
    }

    /// <summary>The untracked path <c>roadmap/../tools/unsafe.json</c> makes capture throw that an unsafe repository path was detected.</summary>
    [Fact]
    public void Path_traversal_is_rejected()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        Action action = () => RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Untracked("roadmap/../tools/unsafe.json"));
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation detected an unsafe repository path.");
    }

    /// <summary>The untracked absolute path <c>/tmp/unsafe.json</c> makes capture throw that an unsafe repository path was detected.</summary>
    [Fact]
    public void Absolute_paths_are_rejected()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        Action action = () => RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Untracked("/tmp/unsafe.json"));
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation detected an unsafe repository path.");
    }

    /// <summary>The untracked path <c>roadmap\items\RM-100.json</c> with backslashes makes capture throw the unsafe-path error rather than treating them as separators.</summary>
    [Fact]
    public void Backslash_paths_are_rejected_instead_of_being_reinterpreted()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        Action action = () => RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Untracked("roadmap\\items\\RM-100.json"));
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation detected an unsafe repository path.");
    }

    /// <summary>A status entry whose head, index, and work-tree modes are all <c>120000</c> makes capture throw that symlink changes are rejected.</summary>
    [Fact]
    public void Symlink_modes_are_rejected()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        Action action = () => RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Ordinary(
                "roadmap/items/link.json",
                headMode: "120000",
                indexMode: "120000",
                workTreeMode: "120000"));
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation rejects symlink changes.");
    }

    /// <summary>An untracked file on disk that is a symbolic link makes capture throw that symlink changes are rejected, although the status carries no link mode.</summary>
    [Fact]
    public void Untracked_symbolic_links_are_rejected_even_without_a_git_link_mode()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var linkPath = Path.Combine(workspace.RootPath, "roadmap", "items", "link.json");
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath) ?? workspace.RootPath);
        File.CreateSymbolicLink(linkPath, Path.Combine(workspace.RootPath, "outside.json"));

        // Act
        Action action = () => RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Untracked("roadmap/items/link.json"));
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation rejects symlink changes.");
    }

    /// <summary>A status entry with mode <c>160000</c> and submodule state <c>S.M.</c> makes capture throw that submodule changes are rejected.</summary>
    [Fact]
    public void Submodule_modes_are_rejected()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        Action action = () => RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Ordinary(
                "roadmap/vendor",
                headMode: "160000",
                indexMode: "160000",
                workTreeMode: "160000",
                submodule: "S.M."));
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation rejects submodule changes.");
    }

    /// <summary>A rename record with an empty original path makes capture throw that the NUL-delimited Git status could not be parsed.</summary>
    [Fact]
    public void Invalid_nul_safe_status_records_are_rejected()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        Action action = () => RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Rename("roadmap/items/RM-100.json", string.Empty));
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation could not parse NUL-delimited Git status.");
    }
}
