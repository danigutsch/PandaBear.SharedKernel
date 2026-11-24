using System.Runtime.Versioning;
using System.Text;
using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Runs <c>recipients update</c>, <c>rotate</c>, and <c>edit</c> against the real age, age-keygen, and sops with throwaway keys.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class RealProgramsRewriteTests
{
    private const string File = "secrets/app.sops.yaml";

    [Fact(SkipType = typeof(RealPrograms), SkipUnless = nameof(RealPrograms.AllAvailable), Skip = "age, age-keygen, and sops are not all installed.")]
    public async Task A_rule_change_is_applied_by_update_and_a_removal_is_completed_by_rotate()
    {
        // Arrange
        using var repository = CreateRepository(null);
        var runner = new SystemToolRunner();
        var keys = Directory.CreateDirectory(Path.Combine(repository.Path, "keys")).FullName;
        var identityA = Path.Combine(keys, "a.txt");
        var identityB = Path.Combine(keys, "b.txt");
        var recipientA = RealPrograms.RecipientFrom((await Run(repository.Path, runner, RealPrograms.Find, null, null, "keygen", "--output", identityA, "--force")).Output);
        var recipientB = RealPrograms.RecipientFrom((await Run(repository.Path, runner, RealPrograms.Find, null, null, "keygen", "--output", identityB, "--force")).Output);
        var configuration = Path.Combine(repository.Path, ".config", "sharedkernel-secrets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configuration)!);
        await System.IO.File.WriteAllTextAsync(configuration, TestRecipients.Config(1, recipientA), TestContext.Current.CancellationToken);
        var encrypt = await Run(repository.Path, runner, RealPrograms.Find, Encoding.UTF8.GetBytes("db:\n  password: hunter2\n"), null, "encrypt", File, "--force");

        // Act
        await System.IO.File.WriteAllTextAsync(configuration, TestRecipients.Config(2, recipientA, recipientB), TestContext.Current.CancellationToken);
        var added = await Run(repository.Path, runner, RealPrograms.Find, null, null, "recipients", "update", File, "--identity", identityA, "--force");
        var verifyAdded = await Run(repository.Path, runner, RealPrograms.Find, null, null, "verify");
        var readByB = await Run(repository.Path, runner, RealPrograms.Find, null, null, "decrypt", File + "#db.password", "--identity", identityB);
        await System.IO.File.WriteAllTextAsync(configuration, TestRecipients.Config(1, recipientB), TestContext.Current.CancellationToken);
        var preview = await Run(repository.Path, runner, RealPrograms.Find, null, null, "rotate", File);
        var rotated = await Run(repository.Path, runner, RealPrograms.Find, null, null, "rotate", File, "--identity", identityB, "--force");
        var verifyRotated = await Run(repository.Path, runner, RealPrograms.Find, null, null, "verify");
        var readByA = await Run(repository.Path, runner, RealPrograms.Find, null, null, "decrypt", File, "--identity", identityA);
        var stillReadByB = await Run(repository.Path, runner, RealPrograms.Find, null, null, "decrypt", File + "#db.password", "--identity", identityB);

        // Assert
        encrypt.ExitCode.ShouldBe(0, encrypt.Error);
        added.ExitCode.ShouldBe(0, added.Error);
        added.Output.ShouldContain($"add recipient {recipientB}", StringComparison.Ordinal);
        verifyAdded.ExitCode.ShouldBe(0, verifyAdded.Output);
        Encoding.UTF8.GetString(readByB.Plaintext).ShouldBe("hunter2");
        preview.Output.ShouldContain($"remove recipient {recipientA}", StringComparison.Ordinal);
        rotated.ExitCode.ShouldBe(0, rotated.Error);
        verifyRotated.ExitCode.ShouldBe(0, verifyRotated.Output);
        readByA.ExitCode.ShouldBe(2);
        readByA.Plaintext.ShouldBeEmpty();
        Encoding.UTF8.GetString(stillReadByB.Plaintext).ShouldBe("hunter2");
    }

    [Fact(SkipType = typeof(RealPrograms), SkipUnless = nameof(RealPrograms.EditAvailable), Skip = "The programs are not all installed, or /dev/shm is not a tmpfs.")]
    [UnsupportedOSPlatform("windows")]
    public async Task Edit_saves_through_real_sops_and_leaves_no_plaintext_in_the_temporary_root()
    {
        // Arrange
        using var repository = CreateRepository(null);
        using var memory = MemoryRoot.Create();
        var runner = new SystemToolRunner();
        var identity = Path.Combine(repository.Path, "a.txt");
        var recipient = RealPrograms.RecipientFrom((await Run(repository.Path, runner, RealPrograms.Find, null, null, "keygen", "--output", identity, "--force")).Output);
        Directory.CreateDirectory(Path.Combine(repository.Path, ".config"));
        await System.IO.File.WriteAllTextAsync(Path.Combine(repository.Path, ".config", "sharedkernel-secrets.json"), TestRecipients.Config(1, recipient), TestContext.Current.CancellationToken);
        await Run(repository.Path, runner, RealPrograms.Find, Encoding.UTF8.GetBytes("db:\n  password: hunter2\n"), null, "encrypt", File, "--force");
        var editor = Path.Combine(repository.Path, "editor.sh");
        await System.IO.File.WriteAllTextAsync(editor, "#!/bin/sh\nsed -i 's/hunter2/correct-horse/' \"$1\"\n", TestContext.Current.CancellationToken);
        System.IO.File.SetUnixFileMode(editor, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var variables = new Dictionary<string, string> { ["EDITOR"] = editor, ["XDG_RUNTIME_DIR"] = memory.Path, ["SOPS_AGE_KEY_FILE"] = identity };

        // Act
        var edit = await RunInTerminal(repository.Path, runner, RealPrograms.Find, variables, "edit", File);
        var value = await Run(repository.Path, runner, RealPrograms.Find, null, variables, "decrypt", File + "#db.password");
        var verify = await Run(repository.Path, runner, RealPrograms.Find, null, null, "verify");

        // Assert
        edit.ExitCode.ShouldBe(0, edit.Error);
        edit.Output.ShouldBe($"{File}: saved.\n");
        Encoding.UTF8.GetString(value.Plaintext).ShouldBe("correct-horse");
        verify.ExitCode.ShouldBe(0, verify.Output);
        Directory.EnumerateFileSystemEntries(memory.Path, "*", SearchOption.AllDirectories).ShouldBeEmpty();
        var saved = await ReadText(repository.Path, File);
        saved.ShouldNotContain("correct-horse", StringComparison.Ordinal);
    }
}
