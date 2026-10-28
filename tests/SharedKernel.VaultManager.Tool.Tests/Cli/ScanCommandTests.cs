using PandaBear.Testing;
using PandaBear.VaultManager.Tool.Tests.Pipeline;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Tests the scan command's summary output, report file handling, and refusal to overwrite inputs.</summary>
public sealed class ScanCommandTests : IDisposable
{
    private const string ExportPassword = "fixture export password";
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly TemporaryDirectory temporaryDirectory = new();

    private string DirectoryPath => temporaryDirectory.Path;

    /// <inheritdoc />
    public void Dispose() => temporaryDirectory.Dispose();

    [Fact]
    public async Task Scan_summarizes_findings_and_password_health_on_standard_output()
    {
        // Arrange
        string keeper = CliInputs.CodesAndPasswordInNotes(DirectoryPath, PipelineSecrets.GitHubRecoveryCodes(900));
        string bitwarden = Write(DirectoryPath, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-mail", "Mail", "bob", "short", "https://mail.example.com")));

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", keeper, bitwarden], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Error.ShouldBeEmpty();
        run.Output.ShouldBe("""
            Items after merging duplicates: 2
            Secret findings: 2
              Pre-accepted: 1
              Proposed: 1
              Needs review: 0
            Items with findings: 1
            Password issues: 1
              Weak: 1
            Attachments to upload after the import: 0

            """.ReplaceLineEndings("\n"), StringComparer.Ordinal);
        Directory.GetFiles(DirectoryPath).Length.ShouldBe(2);
    }

    [Fact]
    public async Task Scan_counts_the_files_of_attachment_folders_that_match_no_single_item_among_the_attachments_to_upload()
    {
        // Arrange
        string zip = Write(DirectoryPath, "bitwarden.zip", TwoBanksZip());

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", zip], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldEndWith("\nAttachments to upload after the import: 2\n", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scan_writes_the_full_report_readable_only_by_its_owner_and_replaces_an_old_one()
    {
        // Arrange
        string keeper = CliInputs.CodesAndPasswordInNotes(DirectoryPath, PipelineSecrets.GitHubRecoveryCodes(910));
        string report = Write(DirectoryPath, "report.md", "# Vault report\n\nold report");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(report, OwnerOnly | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", keeper, "--report", report], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldEndWith("Report: " + report + "\n", StringComparison.Ordinal);
        string markdown = await File.ReadAllTextAsync(report, TestContext.Current.CancellationToken);
        markdown.ShouldStartWith("# Vault report\n", StringComparison.Ordinal);
        markdown.ShouldContain("## Secret findings", StringComparison.Ordinal);
        markdown.ShouldContain("## Password health", StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(report).ShouldBe(OwnerOnly);
        }
    }

    [Fact]
    public async Task Scan_never_replaces_an_existing_file_that_is_not_a_vault_report()
    {
        // Arrange
        string keeper = CliInputs.CodesAndPasswordInNotes(DirectoryPath, PipelineSecrets.GitHubRecoveryCodes(950));
        string decisions = Write(DirectoryPath, "decisions.json", "{\"version\":2,\"groups\":[]}");

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", keeper, "--report", decisions], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldBe("The report file '" + decisions + "' exists and is not a vault report. Choose another path, or delete the file first.\n", StringComparer.Ordinal);
        string after = await File.ReadAllTextAsync(decisions, TestContext.Current.CancellationToken);
        after.ShouldBe("{\"version\":2,\"groups\":[]}");
    }

    [Fact]
    public async Task Scan_never_writes_the_report_over_an_input()
    {
        // Arrange
        string keeper = CliInputs.CodesAndPasswordInNotes(DirectoryPath, PipelineSecrets.GitHubRecoveryCodes(920));
        string before = await File.ReadAllTextAsync(keeper, TestContext.Current.CancellationToken);

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", keeper, "--report", keeper], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("may be the same file as an input", StringComparison.Ordinal);
        string after = await File.ReadAllTextAsync(keeper, TestContext.Current.CancellationToken);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Scan_refuses_a_report_path_that_differs_from_an_input_only_in_case_and_says_why()
    {
        // Arrange
        string bitwarden = CliInputs.BitwardenWithFolder(DirectoryPath);
        string report = Path.Combine(DirectoryPath, "BITWARDEN.json");

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", bitwarden, "--report", report], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldBe("The report file '" + report + "' may be the same file as an input file: paths are compared after resolving symbolic links and ignoring case. Choose another path.\n", StringComparer.Ordinal);
    }

    [Fact(SkipType = typeof(FileSystemProbes), SkipUnless = nameof(FileSystemProbes.CanCreateSymbolicLinks), Skip = "This system does not let the test create symbolic links.")]
    public async Task Scan_never_writes_the_report_over_an_input_reached_through_a_linked_directory()
    {
        // Arrange
        string real = Directory.CreateDirectory(Path.Combine(DirectoryPath, "real")).FullName;
        string keeper = CliInputs.CodesAndPasswordInNotes(real, PipelineSecrets.GitHubRecoveryCodes(940));
        string alias = Path.Combine(DirectoryPath, "alias");
        Directory.CreateSymbolicLink(alias, real);
        string before = await File.ReadAllTextAsync(keeper, TestContext.Current.CancellationToken);

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", keeper, "--report", Path.Combine(alias, "keeper.json")], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldContain("may be the same file as an input", StringComparison.Ordinal);
        string after = await File.ReadAllTextAsync(keeper, TestContext.Current.CancellationToken);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Scan_fails_before_reading_inputs_when_the_report_directory_does_not_exist()
    {
        // Arrange
        string keeper = CliInputs.CodesAndPasswordInNotes(DirectoryPath, PipelineSecrets.GitHubRecoveryCodes(930));
        string report = Path.Combine(DirectoryPath, "missing", "report.md");

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", keeper, "--report", report], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldContain("does not exist", StringComparison.Ordinal);
    }
}
