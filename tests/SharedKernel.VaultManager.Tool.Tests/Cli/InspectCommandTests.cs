using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

public sealed class InspectCommandTests : IDisposable
{
    private const string ExportPassword = "fixture export password";
    private const string FilePassword = "fixture file password";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-cli-inspect-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task Inspect_reports_format_counts_folders_times_attachments_and_warnings_per_input()
    {
        string zip = CliInputs.KeeperZipWithFolderAndAttachments(directory);
        string bitwarden = CliInputs.BitwardenWithFolder(directory);

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", zip, bitwarden], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        run.Error.ShouldBeEmpty();
        run.Output.ShouldBe("""
            keeper.zip
              Format: Keeper Commander zip
              Items: 2
                Login: 1
                Secure note: 1
              Items with a modification time: 2
              Items without a modification time: 0
              Folders: 1
                Personal
              Attachments in the export: 1
              Attachments missing from the export: 1
              Warnings: 1
                "Backup": attachment "photo.png" is listed but not included in the zip.

            bitwarden.json
              Format: Bitwarden JSON
              Items: 2
                Login: 1
                Secure note: 1
              Items with a modification time: 1
              Items without a modification time: 1
              Folders: 1
                Work
              Attachments in the export: 0
              Attachments missing from the export: 0
              Warnings: 0

            """.ReplaceLineEndings("\n"), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Inspect_asks_once_for_the_password_of_a_protected_input()
    {
        string input = CliInputs.ProtectedBitwarden(directory, FilePassword);
        var prompts = new TestPrompts(ExportPassword, FilePassword);

        var run = await CommandLineRun.Invoke(prompts.Environment(), ["inspect", input], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Format: Bitwarden password-protected JSON", StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBe(["Password for protected.json"]);
    }

    [Fact]
    public async Task Inspect_reports_the_readable_inputs_and_exits_3_for_an_unreadable_one()
    {
        string bitwarden = CliInputs.BitwardenWithFolder(directory);
        string missing = Path.Combine(directory, "missing.json");

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", missing, bitwarden], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Output.ShouldStartWith("bitwarden.json\n", StringComparison.Ordinal);
        run.Output.ShouldNotContain("missing.json", StringComparison.Ordinal);
        run.Error.ShouldStartWith("missing.json: ", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_takes_an_argument_starting_with_at_as_a_file_name_and_shows_no_vault_values()
    {
        string export = CliInputs.BitwardenWithFolder(directory);

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", "@" + export], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldContain("@" + export, StringComparison.Ordinal);
        string[] values = [CliInputs.BitwardenPassword, "fixture wifi note"];
        values.ShouldAllSatisfy(value => run.Error.ShouldNotContain(value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Inspect_replaces_control_characters_in_folder_names()
    {
        string folder = string.Concat("Work", (char)0x1B, "[2J", (char)0x9B, "31m", (char)0x7F, (char)0x07, "end");
        string bitwarden = CliInputs.BitwardenWithFolder(directory, folder);

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", bitwarden], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("\n    Work\uFFFD[2J\uFFFD31m\uFFFD\uFFFDend\n", StringComparison.Ordinal);
        char[] controls = [(char)0x1B, (char)0x9B, (char)0x7F, (char)0x07];
        controls.ShouldAllSatisfy(control => run.Output.ShouldNotContain(control));
    }

    [Theory]
    [InlineData("export.txt", "not an export\n", "not recognized")]
    [InlineData("restricted.json", """{"encrypted":true,"encKeyValidation_DO_NOT_EDIT":"2.x","data":"2.x"}""", "account-restricted")]
    public async Task Inspect_names_an_input_it_cannot_read(string name, string content, string reason)
    {
        string input = PipelineInputs.Write(directory, name, content);

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", input], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldStartWith(name + ": ", StringComparison.Ordinal);
        run.Error.ShouldContain(reason, StringComparison.Ordinal);
    }
}
