using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Tests <c>recipients check --prove-with-identity</c> and <c>--prove-absent-identity</c> with a fake sops.</summary>
[Trait("Capability", "Secrets")]
public sealed class ProveCommandTests
{
    private static readonly string Config = TestRecipients.Config(2, TestRecipients.First, TestRecipients.Second);

    [Fact]
    public async Task An_identity_that_can_decrypt_is_reported_and_the_plaintext_is_never_shown()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        var runner = ToolScripts.Decrypting("/keys/a.txt");

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", "/keys/a.txt");

        // Assert
        var visible = run.Output + run.Error;
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("/keys/a.txt: can decrypt", StringComparison.Ordinal);
        visible.ShouldNotContain("SECRET-PLAINTEXT", StringComparison.Ordinal);
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Arguments.Take(2).ToArray().ShouldBe(["--config", "/dev/null"]);
        request.Environment.ShouldBe(new Dictionary<string, string> { ["SOPS_AGE_KEY_FILE"] = "/keys/a.txt" });
        request.WorkingDirectory.ShouldNotBeNull();
    }

    [Fact]
    public async Task An_identity_that_cannot_decrypt_makes_the_check_fail_with_exit_code_one()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();

        // Act
        var run = await Run(repository.Path, ToolScripts.Decrypting(), ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", "/keys/a.txt");

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain("/keys/a.txt: cannot decrypt", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_identity_that_should_be_absent_and_cannot_decrypt_passes_when_a_holder_could()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();

        // Act
        var run = await Run(repository.Path, ToolScripts.Decrypting("/keys/a.txt"), ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", "/keys/a.txt", "--prove-absent-identity", "/keys/revoked.txt");

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("/keys/revoked.txt: cannot decrypt, as required", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_identity_that_should_be_absent_but_can_decrypt_fails_the_check()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();

        // Act
        var run = await Run(repository.Path, ToolScripts.Decrypting("/keys/a.txt", "/keys/revoked.txt"), ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", "/keys/a.txt", "--prove-absent-identity", "/keys/revoked.txt");

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain("/keys/revoked.txt: CAN decrypt but must not", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_absence_proof_without_a_holder_that_decrypts_is_meaningless_and_refused()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        var runner = ToolScripts.Decrypting();

        // Act
        var withoutHolder = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-absent-identity", "/keys/revoked.txt");
        var failingHolder = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", "/keys/a.txt", "--prove-absent-identity", "/keys/revoked.txt");

        // Assert
        withoutHolder.ExitCode.ShouldBe(2);
        withoutHolder.Error.ShouldContain("--prove-with-identity", StringComparison.Ordinal);
        failingHolder.ExitCode.ShouldBe(1);
        failingHolder.Output.ShouldNotContain("as required", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Key_material_given_as_an_identity_is_refused_without_echoing_it_and_nothing_runs()
    {
        // Arrange
        using var repository = ToolScripts.ProveRepository();
        const string key = "AGE-SECRET-KEY-1QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ";
        var runner = ScriptedToolRunner.Unused();

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", key);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldNotContain("QQQQ", StringComparison.Ordinal);
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_sops_is_a_failure_and_a_file_that_differs_still_exits_one_without_proofs_run()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/subset.sops.yaml", TestRecipients.Yaml([TestRecipients.First]));
        WriteFile(repository.Path, "secrets/app.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        var runner = ToolScripts.Decrypting("/keys/a.txt");

        // Act
        var missing = await Run(repository.Path, ScriptedToolRunner.Unused(), ResolveNone, null, null, "recipients", "check", "secrets/app.sops.yaml", "--root", repository.Path, "--prove-with-identity", "/keys/a.txt");
        var differs = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "check", "secrets/subset.sops.yaml", "--root", repository.Path, "--prove-with-identity", "/keys/a.txt");

        // Assert
        missing.ExitCode.ShouldBe(2);
        differs.ExitCode.ShouldBe(1);
        differs.Output.ShouldContain("missing recipient", StringComparison.Ordinal);
        differs.Output.ShouldContain("/keys/a.txt: can decrypt", StringComparison.Ordinal);
    }
}
