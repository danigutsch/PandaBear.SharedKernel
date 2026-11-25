namespace SharedKernel.RepoConfig.Tests;

/// <summary>Exercises publication policy through the real command and Git index.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class PublicationGuardCommandTests
{
    [Fact]
    public async Task New_can_seed_the_guard_and_ignore_the_private_patterns_file()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var result = await NewTestOperations.Run(workspace, "--with", "publication-guard", "--force");

        // Assert
        result.ExitCode.ShouldBe(0);
        workspace.FileExists(PublicationGuardTestRepository.PolicyPath).ShouldBeTrue();
        workspace.ReadFile(".config/.gitignore").ShouldBe("/publication-guard.local.patterns\n");
        workspace.FileExists(PublicationGuardTestRepository.LocalPatternsPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Verify_publication_rejects_a_forbidden_tracked_path()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await PublicationGuardTestRepository.Create(PublicationGuardTestRepository.Policy, ct);
        await PublicationGuardTestRepository.Stage(repository, "private/owner-note.txt", "synthetic content", ct);

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("forbidden path", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("owner-note", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("note.txt", PublicationGuardTestRepository.Pattern)]
    [InlineData("synthetic-private-marker.txt", "ordinary content")]
    public async Task Verify_publication_rejects_a_staged_pattern_without_echoing_matches(string path, string content)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await PublicationGuardTestRepository.Create(PublicationGuardTestRepository.Policy, ct);
        await PublicationGuardTestRepository.Stage(repository, path, content, ct);
        repository.WriteText(path, "working tree replacement");

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("configured personal-data pattern", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(PublicationGuardTestRepository.Pattern, StringComparison.Ordinal);
        result.StandardOutput.ShouldBeEmpty();
    }

    [Fact]
    public async Task Verify_publication_rejects_an_allowlist_entry_without_a_reason()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var policy = PublicationGuardTestRepository.Policy.Replace(
            """ "allowlist": [] """.Trim(),
            """ "allowlist": [{"rule": "marker", "path": "note.txt"}] """.Trim(),
            StringComparison.Ordinal);
        using var repository = await PublicationGuardTestRepository.Create(policy, ct);

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("requires a known rule, exact path, and reason", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_publication_reports_a_skip_for_the_absent_local_patterns_file()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await PublicationGuardTestRepository.Create(PublicationGuardTestRepository.Policy, ct);
        await PublicationGuardTestRepository.Stage(repository, "ordinary.txt", "tracked content", ct);
        repository.WriteText("ordinary.txt", PublicationGuardTestRepository.Pattern);
        repository.WriteText(PublicationGuardTestRepository.PolicyPath, "{}");

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldContain("skip: optional local patterns file is absent", StringComparison.Ordinal);
        result.StandardOutput.ShouldContain("Publication guard passed.", StringComparison.Ordinal);
        result.StandardError.ShouldBeEmpty();
    }

    [Fact]
    public async Task Verify_publication_keeps_local_patterns_private()
    {
        // Arrange
        const string privateMarker = "synthetic-untracked-local-value";
        var ct = TestContext.Current.CancellationToken;
        using var repository = await PublicationGuardTestRepository.Create(PublicationGuardTestRepository.Policy, ct);
        await PublicationGuardTestRepository.Stage(repository, "note.txt", privateMarker, ct);
        repository.WriteText(PublicationGuardTestRepository.LocalPatternsPath, privateMarker);

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("local personal-data pattern", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(privateMarker, StringComparison.Ordinal);
        result.StandardOutput.ShouldBeEmpty();
    }

    [Fact]
    public async Task Verify_publication_rejects_a_tracked_local_patterns_file()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await PublicationGuardTestRepository.Create(PublicationGuardTestRepository.Policy, ct);
        await PublicationGuardTestRepository.Stage(repository, PublicationGuardTestRepository.LocalPatternsPath, "# synthetic", ct);

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("local patterns file must not be tracked", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_publication_applies_a_reasoned_allowlist_to_one_rule_and_path()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var policy = PublicationGuardTestRepository.Policy.Replace(
            """ "allowlist": [] """.Trim(),
            """ "allowlist": [{"rule": "marker", "path": "note.txt", "reason": "Synthetic fixture."}] """.Trim(),
            StringComparison.Ordinal);
        using var repository = await PublicationGuardTestRepository.Create(policy, ct);
        await PublicationGuardTestRepository.Stage(repository, "note.txt", PublicationGuardTestRepository.Pattern, ct);

        // Act
        var allowed = await repository.RunCommand(["verify-publication"], ct);
        await PublicationGuardTestRepository.Stage(repository, "other.txt", PublicationGuardTestRepository.Pattern, ct);
        var other = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        allowed.ExitCode.ShouldBe(0);
        other.ExitCode.ShouldBe(1);
        other.StandardError.ShouldContain("configured personal-data pattern", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_publication_scans_a_staged_symbolic_link_target()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await PublicationGuardTestRepository.Create(PublicationGuardTestRepository.Policy, ct);
        repository.WriteText("untracked-target.txt", PublicationGuardTestRepository.Pattern);
        var target = await repository.StoreBlob("untracked-target.txt", ct);
        await repository.SetIndexEntry("120000", target, "ordinary-link", ct);

        // Act
        var result = await repository.RunCommand(["verify-publication"], ct);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("configured personal-data pattern", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(PublicationGuardTestRepository.Pattern, StringComparison.Ordinal);
    }
}
