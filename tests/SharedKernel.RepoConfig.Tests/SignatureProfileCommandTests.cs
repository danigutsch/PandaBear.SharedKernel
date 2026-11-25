using static SharedKernel.RepoConfig.Tests.SignatureProfileTestRepository;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Exercises the managed signature profile against disposable Git histories and signing keys.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class SignatureProfileCommandTests
{
    [Fact]
    public async Task New_seeds_an_owner_editable_signer_file_only_when_selected()
    {
        // Arrange
        using var standard = new TemporaryRepoConfigWorkspace();
        using var selected = new TemporaryRepoConfigWorkspace();

        // Act
        var defaultResult = await NewTestOperations.Run(standard, "--force");
        var selectedResult = await NewTestOperations.Run(selected, "--with", "signature-verifier", "--force");
        var baseline = await NewTestOperations.BaselineVerify(selected);

        // Assert
        defaultResult.ExitCode.ShouldBe(0, defaultResult.Error);
        selectedResult.ExitCode.ShouldBe(0, selectedResult.Error);
        standard.FileExists(SignerPath).ShouldBeFalse();
        selected.ReadFile(SignerPath).ShouldContain("SSH public keys", StringComparison.Ordinal);
        selected.FileExists("README.md").ShouldBeTrue();
        baseline.ExitCode.ShouldBe(0, baseline.Error);
    }

    [Fact]
    public async Task Verifier_uses_the_base_signers_and_explicit_change_head()
    {
        // Arrange
        using var repository = new SignatureProfileTestRepository();
        await repository.Initialize();
        var key = await repository.CreateKey("trusted key");
        repository.WriteSigners(await SignerEntry(key));
        var baseline = await repository.Commit();
        var change = await repository.Commit(key);
        await repository.Git("switch", "--detach", baseline);
        await repository.Git("merge", "--no-ff", "--no-gpg-sign", "--no-edit", change);
        var syntheticMerge = (await repository.Git("rev-parse", "HEAD")).Trim();
        repository.WriteSigners("untrusted working-copy content\n");

        // Act
        var verified = await repository.Verify(baseline, change);
        var wrongHead = await repository.Verify(baseline, syntheticMerge);

        // Assert
        verified.ExitCode.ShouldBe(0, verified.StandardError);
        verified.StandardOutput.ShouldContain("Verified 1 commit(s).", StringComparison.Ordinal);
        verified.StandardError.ShouldBe(string.Empty);
        wrongHead.ExitCode.ShouldBe(1);
        wrongHead.StandardError.ShouldContain($"commit {syntheticMerge} does not have an allowed SSH signature", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verifier_rejects_an_unsigned_commit_after_an_allowed_one()
    {
        // Arrange
        using var repository = new SignatureProfileTestRepository();
        await repository.Initialize();
        var key = await repository.CreateKey("trusted");
        repository.WriteSigners(await SignerEntry(key));
        var baseline = await repository.Commit();
        await repository.Commit(key);
        var unsigned = await repository.Commit();

        // Act
        var result = await repository.Verify(baseline);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardOutput.ShouldBe(string.Empty);
        result.StandardError.ShouldContain($"commit {unsigned} does not have an allowed SSH signature", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verifier_rejects_a_key_added_by_the_change_it_signs()
    {
        // Arrange
        using var repository = new SignatureProfileTestRepository();
        await repository.Initialize();
        var trusted = await repository.CreateKey("trusted");
        var introduced = await repository.CreateKey("introduced");
        var baseSigners = await SignerEntry(trusted);
        repository.WriteSigners(baseSigners);
        var baseline = await repository.Commit();
        repository.WriteSigners(baseSigners + await SignerEntry(introduced));
        await repository.Git("config", "--local", "gpg.ssh.allowedSignersFile", SignerPath);
        var change = await repository.Commit(introduced);

        // Act
        var result = await repository.Verify(baseline);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain($"commit {change} does not have an allowed SSH signature", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing-base", "HEAD", "base ref does not resolve to a commit")]
    [InlineData("--help", "HEAD", "base ref does not resolve to a commit")]
    [InlineData("HEAD", "missing-head", "head ref does not resolve to a commit")]
    [InlineData("HEAD", "HEAD", "no commits to verify")]
    public async Task Verifier_rejects_invalid_or_empty_ranges(string baseRef, string headRef, string expected)
    {
        // Arrange
        using var repository = new SignatureProfileTestRepository();
        await repository.Initialize();
        await repository.Commit();

        // Act
        var result = await repository.Verify(baseRef, headRef);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain(expected, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Verifier_requires_a_nonempty_signer_file_in_the_base_commit(bool emptyFile)
    {
        // Arrange
        using var repository = new SignatureProfileTestRepository();
        await repository.Initialize();
        if (emptyFile)
        {
            repository.WriteSigners(string.Empty);
        }

        var baseline = await repository.Commit();
        var introduced = await repository.CreateKey("introduced");
        repository.WriteSigners(await SignerEntry(introduced));
        await repository.Commit(introduced);

        // Act
        var result = await repository.Verify(baseline);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("base commit must contain a non-empty .config/allowed_signers", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verifier_reports_missing_keygen()
    {
        // Arrange
        using var repository = new SignatureProfileTestRepository();
        await repository.Initialize();

        // Act
        var result = await repository.Verify("HEAD", withoutKeygen: true);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("ssh-keygen is required", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "signature checks require a Git repository")]
    [InlineData(true, "a complete Git history is required")]
    public async Task Verifier_requires_a_repository_with_complete_history(bool shallow, string expected)
    {
        // Arrange
        using var repository = new SignatureProfileTestRepository();
        using var checkout = new TemporaryDirectory();
        if (shallow)
        {
            await repository.Initialize();
            await repository.Commit();
            await repository.Commit();
            await repository.CloneShallow(checkout.Path);
        }
        else
        {
            // Stop Git from discovering an enclosing repository when the temporary root is inside a checkout.
            await File.WriteAllTextAsync(Path.Combine(checkout.Path, ".git"), "gitdir: ./missing-repository\n", TestContext.Current.CancellationToken);
        }

        // Act
        var result = await repository.Verify("HEAD", workTree: checkout.Path);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain(expected, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verifier_checks_key_retirement_at_the_commit_time()
    {
        // Arrange
        using var repository = new SignatureProfileTestRepository();
        await repository.Initialize();
        var key = await repository.CreateKey("retiring");
        repository.WriteSigners(await SignerEntry(key, "valid-before=\"20240102120000Z\" "));
        var baseline = await repository.Commit();
        var beforeRetirement = await repository.Commit(key, "2024-01-01T12:00:00Z");
        var afterRetirement = await repository.Commit(key, "2024-01-03T12:00:00Z");

        // Act
        var accepted = await repository.Verify(baseline, beforeRetirement);
        var rejected = await repository.Verify(baseline, afterRetirement);

        // Assert
        accepted.ExitCode.ShouldBe(0, accepted.StandardError);
        rejected.ExitCode.ShouldBe(1);
        rejected.StandardError.ShouldContain($"commit {afterRetirement} does not have an allowed SSH signature", StringComparison.Ordinal);
    }
}
