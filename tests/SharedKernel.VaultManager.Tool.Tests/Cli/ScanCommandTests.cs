using PandaBear.VaultManager.Tool.Tests.Pipeline;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

public sealed class ScanCommandTests : IDisposable
{
    private const string ExportPassword = "fixture export password";
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string directory = Directory.CreateTempSubdirectory("vault-cli-scan-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task Scan_summarizes_findings_and_password_health_on_standard_output()
    {
        string keeper = CliInputs.CodesAndPasswordInNotes(directory, PipelineSecrets.GitHubRecoveryCodes(900));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-mail", "Mail", "bob", "short", "https://mail.example.com")));

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", keeper, bitwarden], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        run.Error.ShouldBeEmpty();
        run.Output.ShouldBe("""
            Items after merging duplicates: 2
            Secret findings: 2
              Pre-accepted: 1
              Proposed: 1
              Needs review: 0
            Items with findings: 1
            Password issues: 2
              Shorter than 12 characters: 1
              Only one kind of character: 1
            Attachments to upload after the import: 0

            """.ReplaceLineEndings("\n"), StringComparer.Ordinal);
        Directory.GetFiles(directory).Length.ShouldBe(2);
    }

    [Fact]
    public async Task Scan_writes_the_full_report_readable_only_by_its_owner_and_replaces_an_old_one()
    {
        string keeper = CliInputs.CodesAndPasswordInNotes(directory, PipelineSecrets.GitHubRecoveryCodes(910));
        string report = Write(directory, "report.md", "old report");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(report, OwnerOnly | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", keeper, "--report", report], TestContext.Current.CancellationToken);

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
    public async Task Scan_never_writes_the_report_over_an_input()
    {
        string keeper = CliInputs.CodesAndPasswordInNotes(directory, PipelineSecrets.GitHubRecoveryCodes(920));
        string before = await File.ReadAllTextAsync(keeper, TestContext.Current.CancellationToken);

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", keeper, "--report", keeper], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("is also an input", StringComparison.Ordinal);
        string after = await File.ReadAllTextAsync(keeper, TestContext.Current.CancellationToken);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Scan_fails_before_reading_inputs_when_the_report_directory_does_not_exist()
    {
        string keeper = CliInputs.CodesAndPasswordInNotes(directory, PipelineSecrets.GitHubRecoveryCodes(930));
        string report = Path.Combine(directory, "missing", "report.md");

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", keeper, "--report", report], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldContain("does not exist", StringComparison.Ordinal);
    }
}
