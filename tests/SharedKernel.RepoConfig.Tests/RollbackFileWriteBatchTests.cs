namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RollbackFileWriteBatchTests
{
    [Fact]
    public void Apply_rejects_stale_source_content_before_mutating_any_file()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("first.json", "first-original");
        workspace.WriteFile("second.json", "second-original");
        AtomicFileWrite[] writes =
        [
            new(Path.Combine(workspace.RootPath, "first.json"), "first-updated", "first-original"),
            new(Path.Combine(workspace.RootPath, "second.json"), "second-updated", "stale-second")
        ];
        Action apply = () => RollbackFileWriteBatch.Apply(workspace.RootPath, writes);

        // Act
        var exception = apply.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("changed after the write plan was created", StringComparison.Ordinal);
        workspace.ReadFile("first.json").ShouldBe("first-original");
        workspace.ReadFile("second.json").ShouldBe("second-original");
    }

    [Fact]
    public void Apply_commits_all_planned_files()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("existing.json", "original");
        AtomicFileWrite[] writes =
        [
            new(Path.Combine(workspace.RootPath, "existing.json"), "updated", "original"),
            new(Path.Combine(workspace.RootPath, "new.json"), "created", null)
        ];

        // Act
        RollbackFileWriteBatch.Apply(workspace.RootPath, writes);

        // Assert
        workspace.ReadFile("existing.json").ShouldBe("updated");
        workspace.ReadFile("new.json").ShouldBe("created");
        Directory.EnumerateFiles(workspace.RootPath, "*.tmp", SearchOption.AllDirectories).ShouldBeEmpty();
        Directory.EnumerateFiles(workspace.RootPath, "*.bak", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public void Apply_rejects_a_write_outside_the_lock_scope()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        var outsidePath = Path.Combine(outside.RootPath, "outside.json");
        AtomicFileWrite[] writes = [new(outsidePath, "created", null)];
        Action apply = () => RollbackFileWriteBatch.Apply(workspace.RootPath, writes);

        // Act
        var exception = apply.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("outside the repository write scope", StringComparison.Ordinal);
        File.Exists(outsidePath).ShouldBe(false);
    }

    [Fact]
    public void Apply_rejects_a_write_through_a_symbolic_link()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        var linkPath = Path.Combine(workspace.RootPath, "linked");
        Directory.CreateSymbolicLink(linkPath, outside.RootPath);
        var outsidePath = Path.Combine(outside.RootPath, "outside.json");
        AtomicFileWrite[] writes = [new(Path.Combine(linkPath, "outside.json"), "created", null)];
        Action apply = () => RollbackFileWriteBatch.Apply(workspace.RootPath, writes);

        // Act
        var exception = apply.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("symbolic link", StringComparison.Ordinal);
        File.Exists(outsidePath).ShouldBe(false);
    }

    [Fact]
    public void Apply_does_not_replace_a_dangling_symbolic_link_for_a_planned_create()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var destinationPath = Path.Combine(workspace.RootPath, "dangling.json");
        File.CreateSymbolicLink(destinationPath, Path.Combine(workspace.RootPath, "missing-target.json"));
        AtomicFileWrite[] writes = [new(destinationPath, "created", null)];
        Action apply = () => RollbackFileWriteBatch.Apply(workspace.RootPath, writes);

        // Act
        var exception = apply.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("symbolic link", StringComparison.Ordinal);
        new FileInfo(destinationPath).LinkTarget.ShouldNotBeNull();
    }

    [Fact]
    public void Apply_rejects_a_symbolic_link_write_scope()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        var linkPath = Path.Combine(workspace.RootPath, "linked-scope");
        Directory.CreateSymbolicLink(linkPath, outside.RootPath);
        Directory.CreateDirectory(Path.Combine(outside.RootPath, "nested"));
        var outsidePath = Path.Combine(outside.RootPath, "nested", "outside.json");
        AtomicFileWrite[] writes = [new(Path.Combine(linkPath, "nested", "outside.json"), "created", null)];
        Action apply = () => RollbackFileWriteBatch.Apply(linkPath, writes);

        // Act
        var exception = apply.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("write scope must not be a symbolic link", StringComparison.Ordinal);
        File.Exists(outsidePath).ShouldBe(false);
    }

    [Fact]
    public void Apply_rechecks_planning_inputs_after_staging()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("input.json", "original-input");
        workspace.WriteFile("output.json", "original-output");
        var precondition = new ChangingAtomicWritePrecondition(workspace);
        AtomicFileWrite[] writes =
        [
            new(Path.Combine(workspace.RootPath, "output.json"), "updated-output", "original-output")
        ];
        Action apply = () => RollbackFileWriteBatch.Apply(workspace.RootPath, writes, precondition.Verify);

        // Act
        var exception = apply.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("changed after the write plan was created", StringComparison.Ordinal);
        precondition.VerificationCount.ShouldBe(2);
        workspace.ReadFile("output.json").ShouldBe("original-output");
    }

    [Fact]
    public void Apply_verifies_an_empty_batch_before_reporting_success()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        Action apply = () => RollbackFileWriteBatch.Apply(
            workspace.RootPath,
            [],
            static () => throw new InvalidOperationException("stale planning input"));

        // Act
        var exception = apply.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("stale planning input");
    }

    [Fact]
    public void Apply_rejects_a_write_scope_beneath_a_symbolic_link()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        Directory.CreateDirectory(Path.Combine(outside.RootPath, "repository"));
        var linkPath = Path.Combine(workspace.RootPath, "linked-parent");
        Directory.CreateSymbolicLink(linkPath, outside.RootPath);
        var scopePath = Path.Combine(linkPath, "repository");
        var outsidePath = Path.Combine(outside.RootPath, "repository", "outside.json");
        AtomicFileWrite[] writes = [new(Path.Combine(scopePath, "outside.json"), "created", null)];
        Action apply = () => RollbackFileWriteBatch.Apply(scopePath, writes);

        // Act
        var exception = apply.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("write scope traverses a symbolic link", StringComparison.Ordinal);
        File.Exists(outsidePath).ShouldBe(false);
    }

    [Fact]
    public void Apply_rolls_back_prior_replacements_when_a_later_replacement_fails()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("first.json", "first-original");
        Directory.CreateDirectory(Path.Combine(workspace.RootPath, "destination-directory"));
        AtomicFileWrite[] writes =
        [
            new(Path.Combine(workspace.RootPath, "first.json"), "first-updated", "first-original"),
            new(Path.Combine(workspace.RootPath, "destination-directory"), "cannot-replace-directory", null)
        ];
        Action apply = () => RollbackFileWriteBatch.Apply(workspace.RootPath, writes);

        // Act
        apply.ShouldThrow<IOException>();

        // Assert
        workspace.ReadFile("first.json").ShouldBe("first-original");
        Directory.EnumerateFiles(workspace.RootPath, "*.tmp", SearchOption.AllDirectories).ShouldBeEmpty();
        Directory.EnumerateFiles(workspace.RootPath, "*.bak", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public void Apply_removes_prior_staged_files_when_later_staging_fails()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("first.json", "first-original");
        AtomicFileWrite[] writes =
        [
            new(Path.Combine(workspace.RootPath, "first.json"), "first-updated", "first-original"),
            new(Path.Combine(workspace.RootPath, "missing", "second.json"), "second-updated", null)
        ];
        Action apply = () => RollbackFileWriteBatch.Apply(workspace.RootPath, writes);

        // Act
        apply.ShouldThrow<DirectoryNotFoundException>();

        // Assert
        workspace.ReadFile("first.json").ShouldBe("first-original");
        Directory.EnumerateFiles(workspace.RootPath, "*.tmp", SearchOption.AllDirectories).ShouldBeEmpty();
        Directory.EnumerateFiles(workspace.RootPath, "*.bak", SearchOption.AllDirectories).ShouldBeEmpty();
    }
}
