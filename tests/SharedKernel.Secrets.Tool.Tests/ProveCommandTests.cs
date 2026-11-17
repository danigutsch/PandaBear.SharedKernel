using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Tests <c>recipients check --prove-with-identity</c> and <c>--prove-absent-identity</c> with a fake sops and real identity files.</summary>
[Trait("Capability", "Secrets")]
public sealed class ProveCommandTests
{
    private static readonly string Config = TestRecipients.Config(2, TestRecipients.First, TestRecipients.Second);

    [Fact]
    public async Task An_identity_that_can_decrypt_is_reported_and_the_plaintext_is_never_shown()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        var holder = ToolScripts.IdentityFile(repository.Path, "a.txt");
        var runner = ToolScripts.Decrypting(holder);

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", holder);

        // Assert
        var visible = run.Output + run.Error;
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain($"{holder}: can decrypt", StringComparison.Ordinal);
        visible.ShouldNotContain("SECRET-PLAINTEXT", StringComparison.Ordinal);
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Arguments.Take(2).ToArray().ShouldBe(["--config", "/dev/null"]);
        request.Environment["SOPS_AGE_KEY_FILE"].ShouldBe(holder);
        request.WorkingDirectory.ShouldNotBeNull();
    }

    [Fact]
    public async Task An_identity_that_matches_no_recipient_makes_the_check_fail_with_exit_code_one()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        var stranger = ToolScripts.IdentityFile(repository.Path, "stranger.txt");

        // Act
        var run = await Run(repository.Path, ToolScripts.Decrypting(), ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", stranger);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain($"{stranger}: cannot decrypt", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_absent_identity_that_matches_no_recipient_passes_next_to_a_holder_that_decrypts()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        var holder = ToolScripts.IdentityFile(repository.Path, "a.txt");
        var revoked = ToolScripts.IdentityFile(repository.Path, "revoked.txt");

        // Act
        var run = await Run(repository.Path, ToolScripts.Decrypting(holder), ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", holder, "--prove-absent-identity", revoked);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain($"{revoked}: cannot decrypt, as required", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_absent_identity_that_can_decrypt_fails_the_check()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        var holder = ToolScripts.IdentityFile(repository.Path, "a.txt");
        var revoked = ToolScripts.IdentityFile(repository.Path, "revoked.txt");

        // Act
        var run = await Run(repository.Path, ToolScripts.Decrypting(holder, revoked), ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", holder, "--prove-absent-identity", revoked);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain($"{revoked}: CAN decrypt but must not", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_absence_proof_alone_is_refused_and_one_next_to_a_holder_that_fails_is_not_tried()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        var holder = ToolScripts.IdentityFile(repository.Path, "a.txt");
        var revoked = ToolScripts.IdentityFile(repository.Path, "revoked.txt");
        var runner = ToolScripts.Decrypting();

        // Act
        var alone = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-absent-identity", revoked);
        var failingHolder = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", holder, "--prove-absent-identity", revoked);

        // Assert
        alone.ExitCode.ShouldBe(2);
        alone.Error.ShouldContain("--prove-with-identity", StringComparison.Ordinal);
        failingHolder.ExitCode.ShouldBe(1);
        failingHolder.Output.ShouldNotContain("as required", StringComparison.Ordinal);
        failingHolder.Output.ShouldContain("not tried", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing", "does not exist")]
    [InlineData("empty", "contains no age identity")]
    [InlineData("garbage", "is not an age identity file")]
    [InlineData("directory", "is a directory")]
    [InlineData("ssh", "SSH private key")]
    [InlineData("plugin", "plugin identity")]
    [InlineData("truncated", "is not an age identity file")]
    [InlineData("commentsonly", "contains no age identity")]
    public async Task An_identity_file_that_is_not_a_readable_age_identity_is_refused_before_any_trial_for_both_kinds_of_proof(string kind, string reason)
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        var holder = ToolScripts.IdentityFile(repository.Path, "a.txt");
        var path = Path.Combine(repository.Path, "bad.txt");
        ToolScripts.WriteBadIdentity(path, kind);

        var runner = ScriptedToolRunner.Unused();

        // Act
        var absent = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", holder, "--prove-absent-identity", path);
        var with = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", path);

        // Assert
        absent.ExitCode.ShouldBe(2);
        with.ExitCode.ShouldBe(2);
        absent.Error.ShouldContain(path, StringComparison.Ordinal);
        absent.Error.ShouldContain(reason, StringComparison.Ordinal);
        with.Error.ShouldContain(reason, StringComparison.Ordinal);
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_ssh_identity_is_refused_with_a_reason_that_says_it_cannot_be_proven()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        var holder = ToolScripts.IdentityFile(repository.Path, "a.txt");
        var ssh = Path.Combine(repository.Path, "id_ed25519");
        ToolScripts.WriteBadIdentity(ssh, "ssh");

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Unused(), ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", holder, "--prove-absent-identity", ssh);

        // Assert
        run.Error.ShouldContain("SSH identities", StringComparison.Ordinal);
        run.Error.ShouldNotContain("AAAA", StringComparison.Ordinal);
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task An_unreadable_identity_file_is_refused_not_counted_as_absent()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        var holder = ToolScripts.IdentityFile(repository.Path, "a.txt");
        var locked = ToolScripts.IdentityFile(repository.Path, "locked.txt");
        File.SetUnixFileMode(locked, UnixFileMode.None);

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Unused(), ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", holder, "--prove-absent-identity", locked);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("cannot be read", StringComparison.Ordinal);
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_fifo_given_as_an_identity_does_not_hang_and_is_refused()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        var holder = ToolScripts.IdentityFile(repository.Path, "a.txt");
        var fifo = Path.Combine(repository.Path, "pipe");
        using var made = await new SystemToolRunner().Run(new ToolRequest("/usr/bin/mkfifo", [fifo]), TestContext.Current.CancellationToken);

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Unused(), ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", holder, "--prove-absent-identity", fifo);

        // Assert
        made.ExitCode.ShouldBe(0);
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("cannot be read", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Cannot decrypt MAC: Error base64-decoding data: illegal base64 data at input byte 172")]
    [InlineData("Error: cannot operate on non-existent file \"/x\"")]
    [InlineData("yaml: line 1: did not find expected node content")]
    [InlineData("Failed to get the data key required to decrypt the SOPS file.\n    - | failed to load age identities. Errors while loading age")]
    [InlineData("Failed to get the data key required to decrypt the SOPS file.\n    - | age: failed to read header: parsing age header: unexpected intro")]
    public async Task A_failed_run_that_is_not_an_identity_verdict_is_exit_two_for_both_kinds_of_proof_and_says_so(string standardError)
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        var holder = ToolScripts.IdentityFile(repository.Path, "a.txt");
        var revoked = ToolScripts.IdentityFile(repository.Path, "revoked.txt");
        var broken = new ScriptedToolRunner(request => request.Environment["SOPS_AGE_KEY_FILE"] == holder
            ? ScriptedToolRunner.Result(0, "x")
            : ScriptedToolRunner.Result(128, string.Empty, standardError));

        // Act
        var absent = await Run(repository.Path, broken, ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", holder, "--prove-absent-identity", revoked);
        var with = await Run(repository.Path, broken, ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", revoked);

        // Assert
        absent.ExitCode.ShouldBe(2);
        absent.Error.ShouldContain("could not be decrypted (not an identity verdict)", StringComparison.Ordinal);
        with.ExitCode.ShouldBe(2);
        with.Error.ShouldContain("could not be decrypted (not an identity verdict)", StringComparison.Ordinal);
        absent.Output.ShouldNotContain("as required", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Key_material_given_as_an_identity_is_refused_without_echoing_it_and_nothing_runs()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        const string key = FakeKeys.Secret;
        var runner = ScriptedToolRunner.Unused();

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", key);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldNotContain("QQQQ", StringComparison.Ordinal);
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_sops_is_a_failure_and_a_file_that_differs_still_exits_one_with_the_proof_reported()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/subset.sops.yaml", TestRecipients.Yaml([TestRecipients.First]));
        WriteFile(repository.Path, "secrets/app.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        var holder = ToolScripts.IdentityFile(repository.Path, "a.txt");

        // Act
        var missing = await Run(repository.Path, ScriptedToolRunner.Unused(), ResolveNone, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", holder);
        var differs = await Run(repository.Path, ToolScripts.Decrypting(holder), ResolveAll, null, null, "recipients", "check", "secrets/subset.sops.yaml", "--root", repository.Path, "--prove-with-identity", holder);

        // Assert
        missing.ExitCode.ShouldBe(2);
        differs.ExitCode.ShouldBe(1);
        differs.Output.ShouldContain("missing recipient", StringComparison.Ordinal);
        differs.Output.ShouldContain($"{holder}: can decrypt", StringComparison.Ordinal);
    }
}
