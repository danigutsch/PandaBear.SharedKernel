using System.Text;
using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Runs the whole tool against the real age, age-keygen, and sops with throwaway keys. Skips when a program is missing.</summary>
[Trait("Capability", "Secrets")]
public sealed class RealProgramsCommandLineTests
{
    private static readonly string[] KeyNames = ["x.txt", "a.txt", "b.txt"];

    [Fact(SkipType = typeof(RealPrograms), SkipUnless = nameof(RealPrograms.AllAvailable), Skip = "age, age-keygen, and sops are not all installed.")]
    public async Task Keygen_encrypt_decrypt_and_verify_work_end_to_end_and_a_changed_rule_is_a_difference()
    {
        // Arrange
        using var repository = CreateRepository(null);
        var runner = new SystemToolRunner();
        var keysDirectory = Path.Combine(repository.Path, "keys");
        Directory.CreateDirectory(keysDirectory);
        var identityA = Path.Combine(keysDirectory, "a.txt");
        var identityB = Path.Combine(keysDirectory, "b.txt");
        var identityC = Path.Combine(keysDirectory, "c.txt");

        // Act
        var keygenA = await Run(repository.Path, runner, RealPrograms.Find, null, null, "keygen", "--output", identityA, "--force");
        var keygenB = await Run(repository.Path, runner, RealPrograms.Find, null, null, "keygen", "--output", identityB, "--force");
        var keygenC = await Run(repository.Path, runner, RealPrograms.Find, null, null, "keygen", "--output", identityC, "--force");
        var recipientA = RealPrograms.RecipientFrom(keygenA.Output);
        var recipientB = RealPrograms.RecipientFrom(keygenB.Output);
        var recipientC = RealPrograms.RecipientFrom(keygenC.Output);
        Directory.CreateDirectory(Path.Combine(repository.Path, ".config"));
        await File.WriteAllTextAsync(Path.Combine(repository.Path, ".config", "sharedkernel-secrets.json"), TestRecipients.Config(2, recipientA, recipientB), TestContext.Current.CancellationToken);
        var encrypt = await Run(repository.Path, runner, RealPrograms.Find, Encoding.UTF8.GetBytes("db:\n  password: hunter2\n"), null, "encrypt", "secrets/app.sops.yaml", "--force");
        var whole = await Run(repository.Path, runner, RealPrograms.Find, null, null, "decrypt", "secrets/app.sops.yaml", "--identity", identityA);
        var value = await Run(repository.Path, runner, RealPrograms.Find, null, null, "decrypt", "secrets/app.sops.yaml#db.password", "--identity", identityB);
        var list = await Run(repository.Path, runner, RealPrograms.Find, null, null, "recipients", "list", "secrets/app.sops.yaml");
        var exact = await Run(repository.Path, runner, RealPrograms.Find, null, null, "verify");
        await File.WriteAllTextAsync(Path.Combine(repository.Path, ".config", "sharedkernel-secrets.json"), TestRecipients.Config(3, recipientA, recipientB, recipientC), TestContext.Current.CancellationToken);
        var drift = await Run(repository.Path, runner, RealPrograms.Find, null, null, "verify");
        var wrongKey = await Run(repository.Path, runner, RealPrograms.Find, null, null, "decrypt", "secrets/app.sops.yaml", "--identity", identityC);

        // Assert
        keygenA.ExitCode.ShouldBe(0);
        encrypt.ExitCode.ShouldBe(0);
        encrypt.Error.ShouldBe(string.Empty);
        Encoding.UTF8.GetString(whole.Plaintext).ShouldContain("password: hunter2", StringComparison.Ordinal);
        Encoding.UTF8.GetString(value.Plaintext).ShouldBe("hunter2");
        list.Output.ShouldContain($"age: {recipientA}", StringComparison.Ordinal);
        list.Output.ShouldContain($"age: {recipientB}", StringComparison.Ordinal);
        exact.ExitCode.ShouldBe(0);
        drift.ExitCode.ShouldBe(1);
        drift.Output.ShouldContain($"missing recipient {recipientC}", StringComparison.Ordinal);
        wrongKey.ExitCode.ShouldBe(2);
        wrongKey.Plaintext.ShouldBeEmpty();
        var encryptedText = await File.ReadAllTextAsync(Path.Combine(repository.Path, "secrets", "app.sops.yaml"), TestContext.Current.CancellationToken);
        encryptedText.ShouldNotContain("hunter2", StringComparison.Ordinal);
    }

    [Fact(SkipType = typeof(RealPrograms), SkipUnless = nameof(RealPrograms.AllAvailable), Skip = "age, age-keygen, and sops are not all installed.")]
    public async Task A_relabelled_file_passes_the_static_check_but_the_trial_decryption_exposes_it()
    {
        // Arrange
        using var repository = CreateRepository(null);
        var runner = new SystemToolRunner();
        var keys = Path.Combine(repository.Path, "keys");
        Directory.CreateDirectory(keys);
        var identities = KeyNames.Select(name => Path.Combine(keys, name)).ToArray();
        var recipients = new List<string>();
        foreach (var identity in identities)
        {
            var keygen = await Run(repository.Path, runner, RealPrograms.Find, null, null, "keygen", "--output", identity, "--force");
            recipients.Add(RealPrograms.RecipientFrom(keygen.Output));
        }

        // The rule names A and B, but the file is really encrypted to X and B, with X's label rewritten to A.
        var (x, a, b) = (recipients[0], recipients[1], recipients[2]);
        Directory.CreateDirectory(Path.Combine(repository.Path, ".config"));
        await File.WriteAllTextAsync(Path.Combine(repository.Path, ".config", "sharedkernel-secrets.json"), TestRecipients.Config(2, x, b), TestContext.Current.CancellationToken);
        var encrypt = await Run(repository.Path, runner, RealPrograms.Find, Encoding.UTF8.GetBytes("password: hunter2\n"), null, "encrypt", "secrets/app.sops.yaml", "--force");
        var file = Path.Combine(repository.Path, "secrets", "app.sops.yaml");
        await File.WriteAllTextAsync(file, (await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken)).Replace(x, a, StringComparison.Ordinal), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(repository.Path, ".config", "sharedkernel-secrets.json"), TestRecipients.Config(2, a, b), TestContext.Current.CancellationToken);

        // Act
        var staticCheck = await Run(repository.Path, runner, RealPrograms.Find, null, null, "recipients", "check", "secrets/app.sops.yaml");
        var proveHolder = await Run(repository.Path, runner, RealPrograms.Find, null, null, "recipients", "check", "secrets/app.sops.yaml", "--prove-with-identity", identities[2]);
        var proveForged = await Run(repository.Path, runner, RealPrograms.Find, null, null, "recipients", "check", "secrets/app.sops.yaml", "--prove-with-identity", identities[1]);
        var proveAbsent = await Run(repository.Path, runner, RealPrograms.Find, null, null, "recipients", "check", "secrets/app.sops.yaml", "--prove-with-identity", identities[2], "--prove-absent-identity", identities[0]);

        // Assert
        encrypt.ExitCode.ShouldBe(0);
        staticCheck.ExitCode.ShouldBe(0);
        staticCheck.Output.ShouldContain("labels consistent", StringComparison.Ordinal);
        proveHolder.ExitCode.ShouldBe(0);
        proveForged.ExitCode.ShouldBe(1);
        proveForged.Output.ShouldContain("a.txt: cannot decrypt", StringComparison.Ordinal);
        proveAbsent.ExitCode.ShouldBe(1);
        proveAbsent.Output.ShouldContain("x.txt: CAN decrypt but must not", StringComparison.Ordinal);
        var visible = proveAbsent.Output + proveAbsent.Error;
        visible.ShouldNotContain("hunter2", StringComparison.Ordinal);
    }

    [Fact(SkipType = typeof(RealPrograms), SkipUnless = nameof(RealPrograms.AllAvailable), Skip = "age, age-keygen, and sops are not all installed.")]
    public async Task Verify_against_a_real_git_base_passes_until_a_label_is_swapped()
    {
        // Arrange
        if (!RealPrograms.GitAvailable)
        {
            return;
        }

        using var repository = CreateRepository(null);
        var token = TestContext.Current.CancellationToken;
        var runner = new SystemToolRunner();
        var keys = Path.Combine(Path.GetTempPath(), "pandabear-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keys);
        var recipients = new List<string>();
        foreach (var name in KeyNames)
        {
            var keygen = await Run(repository.Path, runner, RealPrograms.Find, null, null, "keygen", "--output", Path.Combine(keys, name), "--force");
            recipients.Add(RealPrograms.RecipientFrom(keygen.Output));
        }

        Directory.CreateDirectory(Path.Combine(repository.Path, ".config"));
        await File.WriteAllTextAsync(Path.Combine(repository.Path, ".config", "sharedkernel-secrets.json"), TestRecipients.Config(2, recipients[1], recipients[2]), token);
        await Run(repository.Path, runner, RealPrograms.Find, Encoding.UTF8.GetBytes("password: hunter2\n"), null, "encrypt", "secrets/app.sops.yaml", "--force");
        var setup = new[] { await RealPrograms.Git(repository.Path, token, "init", "-q"), await RealPrograms.Git(repository.Path, token, "add", "-A"), await RealPrograms.Git(repository.Path, token, "commit", "-q", "-m", "base") };

        // Act
        var unchanged = await Run(repository.Path, runner, RealPrograms.Find, null, null, "verify", "--base", "HEAD");
        var file = Path.Combine(repository.Path, "secrets", "app.sops.yaml");
        var text = await File.ReadAllTextAsync(file, token);
        await File.WriteAllTextAsync(file, text.Replace(recipients[1], recipients[0], StringComparison.Ordinal), token);
        await File.WriteAllTextAsync(Path.Combine(repository.Path, ".config", "sharedkernel-secrets.json"), TestRecipients.Config(2, recipients[0], recipients[2]), token);
        var swapped = await Run(repository.Path, runner, RealPrograms.Find, null, null, "verify", "--base", "HEAD");
        var allowed = await Run(repository.Path, runner, RealPrograms.Find, null, null, "verify", "--base", "HEAD", "--allow-changed-recipients");

        // Assert
        setup.ShouldBe([0, 0, 0]);
        unchanged.ExitCode.ShouldBe(0);
        swapped.ExitCode.ShouldBe(1);
        swapped.Output.ShouldContain($"changed: needs review: recipient {recipients[0]}", StringComparison.Ordinal);
        allowed.ExitCode.ShouldBe(0);
    }

    [Fact(SkipType = typeof(RealPrograms), SkipUnless = nameof(RealPrograms.AllAvailable), Skip = "age, age-keygen, and sops are not all installed.")]
    public async Task Verify_against_a_real_git_base_works_with_the_root_in_a_subdirectory_and_never_compares_with_a_top_level_twin()
    {
        // Arrange
        if (!RealPrograms.GitAvailable)
        {
            return;
        }

        using var outer = CreateRepository(null);
        var token = TestContext.Current.CancellationToken;
        var runner = new SystemToolRunner();
        var root = Path.Combine(outer.Path, "sub");
        Directory.CreateDirectory(Path.Combine(root, ".config"));
        var keys = Path.Combine(Path.GetTempPath(), "pandabear-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keys);
        var recipients = new List<string>();
        foreach (var name in KeyNames)
        {
            var keygen = await Run(outer.Path, runner, RealPrograms.Find, null, null, "keygen", "--output", Path.Combine(keys, name), "--force");
            recipients.Add(RealPrograms.RecipientFrom(keygen.Output));
        }

        await File.WriteAllTextAsync(Path.Combine(root, ".config", "sharedkernel-secrets.json"), TestRecipients.Config(2, recipients[1], recipients[2]), token);
        await Run(root, runner, RealPrograms.Find, Encoding.UTF8.GetBytes("password: hunter2\n"), null, "encrypt", "secrets/app.sops.yaml", "--force");
        var setup = new List<int> { await RealPrograms.Git(outer.Path, token, "init", "-q"), await RealPrograms.Git(outer.Path, token, "add", "-A"), await RealPrograms.Git(outer.Path, token, "commit", "-q", "-m", "base") };

        // Act
        var unchanged = await Run(root, runner, RealPrograms.Find, null, null, "verify", "--base", "HEAD");

        // A top-level twin that an attacker controls: same relative name, labels the rule accepts, committed before the forgery.
        var file = Path.Combine(root, "secrets", "app.sops.yaml");
        var good = await File.ReadAllTextAsync(file, token);
        var forged = good.Replace(recipients[1], recipients[0], StringComparison.Ordinal);
        Directory.CreateDirectory(Path.Combine(outer.Path, "secrets"));
        await File.WriteAllTextAsync(Path.Combine(outer.Path, "secrets", "app.sops.yaml"), forged, token);
        await File.WriteAllTextAsync(Path.Combine(outer.Path, "secrets", "other.sops.yaml"), forged, token);
        setup.Add(await RealPrograms.Git(outer.Path, token, "add", "-A"));
        setup.Add(await RealPrograms.Git(outer.Path, token, "commit", "-q", "-m", "twin"));
        await File.WriteAllTextAsync(file, forged, token);
        await File.WriteAllTextAsync(Path.Combine(root, "secrets", "other.sops.yaml"), forged, token);
        await File.WriteAllTextAsync(Path.Combine(root, ".config", "sharedkernel-secrets.json"), TestRecipients.Config(2, recipients[0], recipients[2]), token);
        var swapped = await Run(root, runner, RealPrograms.Find, null, null, "verify", "--base", "HEAD");

        // Assert
        setup.Count(static code => code != 0).ShouldBe(0);
        unchanged.ExitCode.ShouldBe(0);
        swapped.ExitCode.ShouldBe(1);
        swapped.Output.ShouldContain($"secrets/app.sops.yaml: changed: needs review: recipient {recipients[0]} is new", StringComparison.Ordinal);
        swapped.Output.ShouldContain($"secrets/other.sops.yaml: changed: needs review: recipient {recipients[0]} is new", StringComparison.Ordinal);
    }

    [Fact(SkipType = typeof(RealPrograms), SkipUnless = nameof(RealPrograms.AllAvailable), Skip = "age, age-keygen, and sops are not all installed.")]
    public async Task Real_sops_files_whose_sops_key_is_spelled_in_ways_a_heuristic_misses_are_never_skipped()
    {
        // Arrange
        using var repository = CreateRepository(null);
        var token = TestContext.Current.CancellationToken;
        var runner = new SystemToolRunner();
        var keys = Path.Combine(Path.GetTempPath(), "pandabear-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keys);
        var recipients = new List<string>();
        foreach (var name in KeyNames)
        {
            var keygen = await Run(repository.Path, runner, RealPrograms.Find, null, null, "keygen", "--output", Path.Combine(keys, name), "--force");
            recipients.Add(RealPrograms.RecipientFrom(keygen.Output));
        }

        Directory.CreateDirectory(Path.Combine(repository.Path, ".config"));
        await File.WriteAllTextAsync(Path.Combine(repository.Path, ".config", "sharedkernel-secrets.json"), TestRecipients.Config(2, recipients[1], recipients[2]), token);
        await Run(repository.Path, runner, RealPrograms.Find, Encoding.UTF8.GetBytes("password: hunter2\n"), null, "encrypt", "secrets/app.sops.yaml", "--force");
        var real = await File.ReadAllTextAsync(Path.Combine(repository.Path, "secrets", "app.sops.yaml"), token);
        var spellings = new Dictionary<string, string> { ["complex"] = "? sops\n:", ["tag"] = "!!str sops:", ["anchor"] = "&x sops:", ["tab"] = "sops:\t" };
        foreach (var (name, spelling) in spellings)
        {
            await File.WriteAllTextAsync(Path.Combine(repository.Path, "secrets", name + ".yaml"), real.Replace("\nsops:", "\n" + spelling, StringComparison.Ordinal), token);
        }

        // Act
        var run = await Run(repository.Path, runner, RealPrograms.Find, null, null, "verify");

        // Assert
        run.ExitCode.ShouldBe(2);
        foreach (var name in spellings.Keys)
        {
            run.Error.ShouldContain($"secrets/{name}.yaml:", StringComparison.Ordinal);
        }
    }
}
