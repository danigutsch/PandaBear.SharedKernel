using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Pipeline;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

public sealed class MergeCommandTests : IDisposable
{
    private const string ExportPassword = "fixture export password";
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string directory = Directory.CreateTempSubdirectory("vault-cli-merge-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task Merge_writes_a_password_protected_export_readable_only_by_its_owner()
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword);

        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", .. inputs, "--output", output], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        run.Error.ShouldBeEmpty();
        run.Output.ShouldEndWith("Wrote the merged vault to " + output + " as a password-protected export.\n", StringComparison.Ordinal);
        prompts.NewPasswordPrompts.ShouldBe([VaultPipeline.NewExportPasswordPrompt]);
        string json = PasswordProtectedExport.Decrypt(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken), ExportPassword);
        var item = BitwardenJsonReader.Read(json, "merged.json").Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Password.ShouldBe(CliInputs.BitwardenPassword);
        item.PasswordHistory.ShouldHaveSingleItem().Password.ShouldBe(CliInputs.KeeperPassword);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(output).ShouldBe(OwnerOnly);
        }
    }

    [Fact]
    public async Task Merge_with_plaintext_writes_unencrypted_json_without_asking_for_a_password()
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword);

        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", .. inputs, "-o", output, "--plaintext"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldEndWith("Wrote the merged vault to " + output + " as unencrypted JSON. Delete it after the import.\n", StringComparison.Ordinal);
        prompts.NewPasswordPrompts.ShouldBeEmpty();
        FormatDetector.Detect(output).ShouldBe(VaultFileFormat.BitwardenJson);
    }

    [Fact]
    public async Task Merge_summarizes_the_merge_on_standard_output()
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--plaintext"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldStartWith("""
            Items in the merged vault: 1
            Merged groups: 1
            Conflicts resolved by the policy: 1
            Fields that need a decision: 0
            Secret findings: 0
              Applied: 0
              Need a decision: 0
            Password issues: 0
            Attachments to upload after the import: 0

            """.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Merge_refuses_to_replace_an_existing_output_without_force_before_reading_inputs()
    {
        string missing = Path.Combine(directory, "missing.json");
        string output = Write(directory, "merged.json", "existing");
        var prompts = new TestPrompts(ExportPassword);

        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", missing, "--output", output], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("already exists", StringComparison.Ordinal);
        run.Error.ShouldContain("--force", StringComparison.Ordinal);
        string kept = await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken);
        kept.ShouldBe("existing");
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Merge_with_force_replaces_an_existing_output_readable_only_by_its_owner()
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Write(directory, "merged.json", "existing");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(output, OwnerOnly | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--plaintext", "--force"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        FormatDetector.Detect(output).ShouldBe(VaultFileFormat.BitwardenJson);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(output).ShouldBe(OwnerOnly);
        }
    }

    [Fact]
    public async Task Merge_never_writes_over_an_input_even_with_force()
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string before = await File.ReadAllTextAsync(inputs[1], TestContext.Current.CancellationToken);

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", inputs[1], "--plaintext", "--force"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("may be the same file as an input", StringComparison.Ordinal);
        string after = await File.ReadAllTextAsync(inputs[1], TestContext.Current.CancellationToken);
        after.ShouldBe(before);
    }

    [Theory]
    [InlineData("--report")]
    [InlineData("--decisions")]
    public async Task Merge_never_writes_a_report_or_decisions_over_an_input_or_the_output(string option)
    {
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string output = Path.Combine(directory, "merged.json");
        string before = await File.ReadAllTextAsync(inputs[0], TestContext.Current.CancellationToken);

        var overInput = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, option, inputs[0]], TestContext.Current.CancellationToken);
        var overOutput = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, option, output], TestContext.Current.CancellationToken);

        overInput.ExitCode.ShouldBe(3);
        overInput.Error.ShouldContain("may be the same file as an input", StringComparison.Ordinal);
        overOutput.ExitCode.ShouldBe(3);
        overOutput.Error.ShouldContain("may be the same file as the output", StringComparison.Ordinal);
        string after = await File.ReadAllTextAsync(inputs[0], TestContext.Current.CancellationToken);
        after.ShouldBe(before);
        File.Exists(output).ShouldBeFalse();
    }

    [Theory(SkipType = typeof(FileSystemProbes), SkipUnless = nameof(FileSystemProbes.CanCreateSymbolicLinks), Skip = "This system does not let the test create symbolic links.")]
    [InlineData("--report")]
    [InlineData("--decisions")]
    public async Task Merge_never_writes_a_report_or_decisions_over_an_input_or_the_output_reached_through_a_linked_directory(string option)
    {
        string exports = Directory.CreateDirectory(Path.Combine(directory, "exports")).FullName;
        string[] inputs = CliInputs.ConflictingLogins(exports);
        string alias = Path.Combine(directory, "alias");
        Directory.CreateSymbolicLink(alias, exports);
        string output = Path.Combine(exports, "merged.json");
        string before = await File.ReadAllTextAsync(inputs[0], TestContext.Current.CancellationToken);

        var overInput = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, option, Path.Combine(alias, "keeper.json")], TestContext.Current.CancellationToken);
        var overOutput = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, option, Path.Combine(alias, "merged.json")], TestContext.Current.CancellationToken);

        overInput.ExitCode.ShouldBe(3);
        overInput.Error.ShouldContain("may be the same file as an input", StringComparison.Ordinal);
        overOutput.ExitCode.ShouldBe(3);
        overOutput.Error.ShouldContain("may be the same file as the output", StringComparison.Ordinal);
        string after = await File.ReadAllTextAsync(inputs[0], TestContext.Current.CancellationToken);
        after.ShouldBe(before);
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Merge_that_cannot_write_the_report_after_the_vault_says_the_vault_was_written_and_exits_3()
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        string reports = Directory.CreateDirectory(Path.Combine(directory, "reports")).FullName;
        string report = Path.Combine(reports, "report.md");

        // The report directory disappears while the vault is written, after the checks that run before any work.
        var prompts = new TestPrompts(ExportPassword) { OnNewPassword = () => Directory.Delete(reports) };

        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", .. inputs, "--output", output, "--report", report], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Output.ShouldContain("Wrote the merged vault to " + output + " as a password-protected export.\n", StringComparison.Ordinal);
        run.Output.ShouldNotContain("Report: ", StringComparison.Ordinal);
        run.Error.ShouldContain("The merged vault was written to " + output + ", but the report was not written: ", StringComparison.Ordinal);
        string json = PasswordProtectedExport.Decrypt(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken), ExportPassword);
        BitwardenJsonReader.Read(json, "merged.json").Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Merge_fails_before_reading_inputs_when_the_output_directory_does_not_exist()
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "missing", "merged.json");
        var prompts = new TestPrompts(ExportPassword);

        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", .. inputs, "--output", output], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldContain("does not exist", StringComparison.Ordinal);
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Merge_does_not_write_when_bitwarden_would_reject_the_import()
    {
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", new string('n', 800), "alice", CliInputs.BitwardenPassword, "https://example.com")));
        string output = Path.Combine(directory, "merged.json");
        string report = Path.Combine(directory, "report.md");
        var prompts = new TestPrompts(ExportPassword);

        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", input, "--output", output, "--report", report], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("name is too long", StringComparison.Ordinal);
        run.Error.ShouldContain("not written", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
        string markdown = await File.ReadAllTextAsync(report, TestContext.Current.CancellationToken);
        markdown.ShouldContain("Bitwarden would reject the import", StringComparison.Ordinal);
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("missing.json", null, "missing.json")]
    [InlineData("export.txt", "not an export\n", "not recognized")]
    [InlineData("restricted.json", """{"encrypted":true,"encKeyValidation_DO_NOT_EDIT":"2.x","data":"2.x"}""", "account-restricted")]
    public async Task Merge_names_an_input_it_cannot_read_before_asking_for_passwords(string name, string? content, string reason)
    {
        string input = content is null ? Path.Combine(directory, name) : Write(directory, name, content);
        string other = CliInputs.ProtectedBitwarden(directory, "fixture file password");
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword, "fixture file password");

        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", other, input, "--output", output], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Error.ShouldStartWith(name + ": ", StringComparison.Ordinal);
        run.Error.ShouldContain(reason, StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Merge_takes_conflicting_values_from_the_preferred_input()
    {
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string output = Path.Combine(directory, "merged.json");

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--plaintext", "--policy", "prefer:keeper.json"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        var item = BitwardenJsonReader.Read(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken), "merged.json").Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Password.ShouldBe(CliInputs.KeeperPassword);
        item.PasswordHistory.ShouldHaveSingleItem().Password.ShouldBe(CliInputs.BitwardenPassword);
    }

    [Fact]
    public async Task Merge_writes_the_report_readable_only_by_its_owner()
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        string report = Path.Combine(directory, "report.md");

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--plaintext", "--report", report], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Report: " + report + "\n", StringComparison.Ordinal);
        string markdown = await File.ReadAllTextAsync(report, TestContext.Current.CancellationToken);
        markdown.ShouldContain("The merged vault was written to `" + output + "` as unencrypted JSON.", StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(report).ShouldBe(OwnerOnly);
        }
    }
}
