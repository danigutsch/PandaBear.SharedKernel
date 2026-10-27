using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Tests the inspect command.</summary>
public sealed class InspectCommandTests : IDisposable
{
    private const string ExportPassword = "fixture export password";
    private const string FilePassword = "fixture file password";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-cli-inspect-").FullName;

    /// <summary>Deletes the temporary directory.</summary>
    public void Dispose() => Directory.Delete(directory, recursive: true);

    /// <summary>Verifies that inspect reports the format, counts, folders, times, attachments, and warnings of each input.</summary>
    [Fact]
    public async Task Inspect_reports_format_counts_folders_times_attachments_and_warnings_per_input()
    {
        // Arrange
        string zip = CliInputs.KeeperZipWithFolderAndAttachments(directory);
        string bitwarden = CliInputs.BitwardenWithFolder(directory);

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", zip, bitwarden], TestContext.Current.CancellationToken);

        // Assert
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
              Folders without items: 0
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
              Folders without items: 0
              Attachments in the export: 0
              Attachments missing from the export: 0
              Warnings: 0

            """.ReplaceLineEndings("\n"), StringComparer.Ordinal);
    }

    /// <summary>Verifies that inspect lists declared folders that hold no item.</summary>
    [Fact]
    public async Task Inspect_lists_declared_folders_that_hold_no_item()
    {
        // Arrange
        string input = PipelineInputs.Write(directory, "bitwarden.json", """{"encrypted":false,"folders":[{"id":"f","name":"Archive/Old"}],"items":[]}""");

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", input], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("  Folders: 1\n    Archive/Old\n  Folders without items: 1\n", StringComparison.Ordinal);
    }

    /// <summary>Verifies that inspect asks once for the password of a protected input.</summary>
    [Fact]
    public async Task Inspect_asks_once_for_the_password_of_a_protected_input()
    {
        // Arrange
        string input = CliInputs.ProtectedBitwarden(directory, FilePassword);
        var prompts = new TestPrompts(ExportPassword, FilePassword);

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), ["inspect", input], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Format: Bitwarden password-protected JSON", StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBe(["Password for protected.json"]);
    }

    /// <summary>Verifies that inspect reports the readable inputs and exits 3 because of an unreadable one.</summary>
    [Fact]
    public async Task Inspect_reports_the_readable_inputs_and_exits_3_for_an_unreadable_one()
    {
        // Arrange
        string bitwarden = CliInputs.BitwardenWithFolder(directory);
        string missing = Path.Combine(directory, "missing.json");

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", missing, bitwarden], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Output.ShouldStartWith("bitwarden.json\n", StringComparison.Ordinal);
        run.Output.ShouldNotContain("missing.json", StringComparison.Ordinal);
        run.Error.ShouldStartWith("missing.json: ", StringComparison.Ordinal);
    }

    /// <summary>Verifies that inspect takes an argument starting with <c>@</c> as a file name and shows no vault values.</summary>
    [Fact]
    public async Task Inspect_takes_an_argument_starting_with_at_as_a_file_name_and_shows_no_vault_values()
    {
        // Arrange
        string export = CliInputs.BitwardenWithFolder(directory);

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", "@" + export], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldContain("@" + export, StringComparison.Ordinal);
        string[] values = [CliInputs.BitwardenPassword, "fixture wifi note"];
        values.ShouldAllSatisfy(value => run.Error.ShouldNotContain(value, StringComparison.Ordinal));
    }

    /// <summary>Verifies that inspect replaces control characters in folder names.</summary>
    [Fact]
    public async Task Inspect_replaces_control_characters_in_folder_names()
    {
        // Arrange
        string folder = string.Concat("Work", (char)0x1B, "[2J", (char)0x9B, "31m", (char)0x7F, (char)0x07, "end");
        string bitwarden = CliInputs.BitwardenWithFolder(directory, folder);

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", bitwarden], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("\n    Work\uFFFD[2J\uFFFD31m\uFFFD\uFFFDend\n", StringComparison.Ordinal);
        char[] controls = [(char)0x1B, (char)0x9B, (char)0x7F, (char)0x07];
        controls.ShouldAllSatisfy(control => run.Output.ShouldNotContain(control));
    }

    /// <summary>Verifies that inspect names an input it cannot read.</summary>
    [Theory]
    [InlineData("export.txt", "not an export\n", "not recognized")]
    [InlineData("restricted.json", """{"encrypted":true,"encKeyValidation_DO_NOT_EDIT":"2.x","data":"2.x"}""", "account-restricted")]
    public async Task Inspect_names_an_input_it_cannot_read(string name, string content, string reason)
    {
        // Arrange
        string input = PipelineInputs.Write(directory, name, content);

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", input], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldStartWith(name + ": ", StringComparison.Ordinal);
        run.Error.ShouldContain(reason, StringComparison.Ordinal);
    }
}
