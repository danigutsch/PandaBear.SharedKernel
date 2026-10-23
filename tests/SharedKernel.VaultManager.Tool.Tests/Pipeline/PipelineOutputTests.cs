using System.Globalization;
using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Pipeline;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

public sealed class PipelineOutputTests : IDisposable
{
    private const string Password = "fixture-output-password";
    private const string ExportPassword = "fixture export password";
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string directory = Directory.CreateTempSubdirectory("vault-pipeline-output-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void Run_writes_a_password_protected_export_unless_plaintext_is_requested()
    {
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com")));
        string encrypted = Path.Combine(directory, "encrypted.json");
        string plaintext = Path.Combine(directory, "plaintext.json");
        var encryptedPrompts = new TestPrompts(ExportPassword);
        var plaintextPrompts = new TestPrompts(ExportPassword);

        var encryptedResult = new VaultPipeline(encryptedPrompts.Environment()).Run(new PipelineRequest([input]) { Output = new PipelineOutput(encrypted) });
        var plaintextResult = new VaultPipeline(plaintextPrompts.Environment()).Run(new PipelineRequest([input]) { Output = new PipelineOutput(plaintext, Plaintext: true) });

        encryptedResult.OutputWritten.ShouldBeTrue();
        plaintextResult.OutputWritten.ShouldBeTrue();
        FormatDetector.Detect(encrypted).ShouldBe(VaultFileFormat.BitwardenJsonPasswordProtected);
        File.ReadAllText(encrypted).ShouldNotContain(Password, StringComparison.Ordinal);
        encryptedPrompts.NewPasswordPrompts.ShouldHaveSingleItem();
        FormatDetector.Detect(plaintext).ShouldBe(VaultFileFormat.BitwardenJson);
        BitwardenJsonReader.Read(File.ReadAllText(plaintext), "plaintext.json").Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Password.ShouldBe(Password);
        plaintextPrompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Fact]
    public void Run_creates_the_output_readable_only_by_its_owner()
    {
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com")));
        string output = Path.Combine(directory, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([input]) { Output = new PipelineOutput(output, Plaintext: true) });

        result.OutputWritten.ShouldBeTrue();
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(output).ShouldBe(OwnerOnly);
        }
    }

    [Fact]
    public void Run_refuses_to_replace_an_existing_output_without_force_before_reading_inputs()
    {
        string missingInput = Path.Combine(directory, "missing.json");
        string output = Write(directory, "merged.json", "existing");
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());

        var action = () => pipeline.Run(new PipelineRequest([missingInput]) { Output = new PipelineOutput(output) });

        var exception = action.ShouldThrow<IOException>();
        exception.Message.ShouldContain("already exists", StringComparison.Ordinal);
        File.ReadAllText(output).ShouldBe("existing");
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Fact]
    public void Run_replaces_an_existing_output_with_force()
    {
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com")));
        string output = Write(directory, "merged.json", "existing");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(output, OwnerOnly | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([input]) { Output = new PipelineOutput(output, Plaintext: true, Force: true) });

        result.OutputWritten.ShouldBeTrue();
        FormatDetector.Detect(output).ShouldBe(VaultFileFormat.BitwardenJson);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(output).ShouldBe(OwnerOnly);
        }
    }

    [Fact]
    public void Run_refuses_to_write_over_an_input_file_even_with_force()
    {
        string json = BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com"));
        string input = Write(directory, "bitwarden.json", json);
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var action = () => pipeline.Run(new PipelineRequest([input]) { Output = new PipelineOutput(input, Plaintext: true, Force: true) });

        action.ShouldThrow<IOException>();
        File.ReadAllText(input).ShouldBe(json);
    }

    [Fact(SkipType = typeof(FileSystemProbes), SkipUnless = nameof(FileSystemProbes.CanCreateSymbolicLinks), Skip = "This system does not let the test create symbolic links.")]
    public void Run_refuses_to_write_over_an_input_reached_through_a_linked_directory_even_with_force()
    {
        string json = BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com"));
        string exports = Directory.CreateDirectory(Path.Combine(directory, "exports")).FullName;
        string input = Write(exports, "bitwarden.json", json);
        string alias = Path.Combine(directory, "alias");
        Directory.CreateSymbolicLink(alias, exports);
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var action = () => pipeline.Run(new PipelineRequest([input]) { Output = new PipelineOutput(Path.Combine(alias, "bitwarden.json"), Plaintext: true, Force: true) });

        action.ShouldThrow<IOException>().Message.ShouldContain("also an input", StringComparison.Ordinal);
        File.ReadAllText(input).ShouldBe(json);
    }

    [Fact(SkipType = typeof(FileSystemProbes), SkipUnless = nameof(FileSystemProbes.CanCreateSymbolicLinks), Skip = "This system does not let the test create symbolic links.")]
    public void Run_refuses_to_write_over_an_input_reached_through_a_link_whose_target_steps_up_from_a_linked_directory_even_with_force()
    {
        string json = BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com"));
        string exports = Directory.CreateDirectory(Path.Combine(directory, "e", "exports")).FullName;
        string nested = Directory.CreateDirectory(Path.Combine(directory, "e", "f")).FullName;
        string links = Directory.CreateDirectory(Path.Combine(directory, "d")).FullName;
        string input = Write(exports, "bitwarden.json", json);
        Directory.CreateSymbolicLink(Path.Combine(links, "nested"), nested);
        string alias = Path.Combine(links, "alias");
        Directory.CreateSymbolicLink(alias, Path.Join(links, "nested", "..", "exports"));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var action = () => pipeline.Run(new PipelineRequest([input]) { Output = new PipelineOutput(Path.Combine(alias, "bitwarden.json"), Plaintext: true, Force: true) });

        action.ShouldThrow<IOException>().Message.ShouldContain("also an input", StringComparison.Ordinal);
        File.ReadAllText(input).ShouldBe(json);
    }

    [Fact]
    public void Run_refuses_to_write_over_an_input_named_with_different_case_even_with_force()
    {
        string json = BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com"));
        string input = Write(directory, "bitwarden.json", json);
        string variant = Path.Combine(directory, "Bitwarden.JSON");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var action = () => pipeline.Run(new PipelineRequest([input]) { Output = new PipelineOutput(variant, Plaintext: true, Force: true) });

        action.ShouldThrow<IOException>().Message.ShouldContain("also an input", StringComparison.Ordinal);
        File.ReadAllText(input).ShouldBe(json);
        Directory.GetFiles(directory).ShouldBe([input]);
    }

    [Fact]
    public void Run_refuses_an_output_in_a_missing_directory_before_reading_inputs()
    {
        string missingInput = Path.Combine(directory, "missing.json");
        string output = Path.Combine(directory, "no-such-directory", "merged.json");
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());

        var action = () => pipeline.Run(new PipelineRequest([missingInput]) { Output = new PipelineOutput(output) });

        var exception = action.ShouldThrow<IOException>();
        exception.Message.ShouldContain("Output directory", StringComparison.Ordinal);
        exception.Message.ShouldContain("no-such-directory", StringComparison.Ordinal);
        exception.Message.ShouldContain("does not exist", StringComparison.Ordinal);
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Fact]
    public void Run_refuses_an_output_path_that_is_a_directory_before_reading_inputs()
    {
        string missingInput = Path.Combine(directory, "missing.json");
        string output = Directory.CreateDirectory(Path.Combine(directory, "merged.json")).FullName;
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());

        var action = () => pipeline.Run(new PipelineRequest([missingInput]) { Output = new PipelineOutput(output, Force: true) });

        action.ShouldThrow<IOException>().Message.ShouldContain("is a directory", StringComparison.Ordinal);
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Fact]
    public void Run_and_merge_reject_an_empty_output_path_as_an_argument_error_before_asking_for_a_password()
    {
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com")));
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());
        var scanned = VaultPipeline.Scan(pipeline.Load([input]));

        var run = () => pipeline.Run(new PipelineRequest([input]) { Output = new PipelineOutput(string.Empty) });
        var merge = () => pipeline.Merge(scanned, new MergeRequest { Output = new PipelineOutput(string.Empty) });

        run.ShouldThrow<ArgumentException>();
        merge.ShouldThrow<ArgumentException>();
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Fact]
    public void Run_refuses_a_new_export_password_shorter_than_the_minimum_before_writing()
    {
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com")));
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts("short");
        var pipeline = new VaultPipeline(prompts.Environment());

        var action = () => pipeline.Run(new PipelineRequest([input]) { Output = new PipelineOutput(output) });

        var exception = action.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain(PasswordReader.MinimumNewPasswordLength.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        prompts.NewPasswordPrompts.ShouldHaveSingleItem();
        Directory.GetFiles(directory).ShouldBe([input]);
    }

    [Fact(SkipType = typeof(FileSystemProbes), SkipUnless = nameof(FileSystemProbes.CanDenyAccess), Skip = "File permissions do not stop this process from reading files.")]
    public void Run_reports_an_input_it_may_not_read_as_a_file_error_that_names_the_input()
    {
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com")));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(input, UnixFileMode.None);
        }

        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var action = () => pipeline.Run(new PipelineRequest([input]));

        var exception = action.ShouldThrow<IOException>();
        exception.Message.ShouldContain(input, StringComparison.Ordinal);
        exception.InnerException.ShouldBeOfType<UnauthorizedAccessException>();
    }

    [Fact(SkipType = typeof(FileSystemProbes), SkipUnless = nameof(FileSystemProbes.CanDenyAccess), Skip = "File permissions do not stop this process from writing files.")]
    public void Run_reports_an_output_directory_it_may_not_write_as_a_file_error_that_names_the_output()
    {
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com")));
        string locked = Directory.CreateDirectory(Path.Combine(directory, "locked")).FullName;
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        string output = Path.Combine(locked, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var action = () => pipeline.Run(new PipelineRequest([input]) { Output = new PipelineOutput(output, Plaintext: true) });

        var exception = action.ShouldThrow<IOException>();
        exception.Message.ShouldContain(output, StringComparison.Ordinal);
        exception.InnerException.ShouldBeOfType<UnauthorizedAccessException>();
        File.Exists(output).ShouldBeFalse();
    }

    [Fact(SkipType = typeof(FileSystemProbes), SkipUnless = nameof(FileSystemProbes.CanDenyAccess), Skip = "File permissions do not stop this process from writing files.")]
    public void Run_reports_an_output_directory_it_may_not_search_as_a_file_error_that_names_the_output()
    {
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", Password, "https://example.com")));
        string locked = Directory.CreateDirectory(Path.Combine(directory, "locked")).FullName;
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(locked, OwnerOnly);
        }

        string output = Path.Combine(locked, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var action = () => pipeline.Run(new PipelineRequest([input]) { Output = new PipelineOutput(output, Plaintext: true) });

        var exception = action.ShouldThrow<IOException>();
        exception.Message.ShouldContain(output, StringComparison.Ordinal);
        exception.InnerException.ShouldBeOfType<UnauthorizedAccessException>();
    }

    [Fact]
    public void Run_does_not_write_when_the_writer_reports_errors()
    {
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", new string('n', 800), "alice", Password, "https://example.com")));
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());

        var result = pipeline.Run(new PipelineRequest([input]) { Output = new PipelineOutput(output) });

        result.HasWriteErrors.ShouldBeTrue();
        result.WriteErrors.ShouldHaveSingleItem().ShouldContain("name is too long", StringComparison.Ordinal);
        result.OutputWritten.ShouldBeFalse();
        File.Exists(output).ShouldBeFalse();
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Fact]
    public void Run_does_not_write_while_conflicts_need_a_decision()
    {
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperLogin("rec-1", "Example", "alice", "fixture-keeper-password", "https://example.com", modified: false)));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", "fixture-bitwarden-password", "https://example.com", revised: false)));
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());

        var result = pipeline.Run(new PipelineRequest([keeper, bitwarden]) { Output = new PipelineOutput(output) });

        result.HasUnresolvedConflicts.ShouldBeTrue();
        result.Unresolved.ShouldHaveSingleItem().FieldPath.ShouldBe("login.password");
        result.HasWriteErrors.ShouldBeFalse();
        result.OutputWritten.ShouldBeFalse();
        File.Exists(output).ShouldBeFalse();
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }
}
