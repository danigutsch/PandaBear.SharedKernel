using System.Text;
using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Runs the whole tool against the real age, age-keygen, and sops with throwaway keys. Skips when a program is missing.</summary>
[Trait("Capability", "Secrets")]
public sealed class RealProgramsCommandLineTests
{
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
}
