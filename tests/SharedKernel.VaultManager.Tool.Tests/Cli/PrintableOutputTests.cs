using PandaBear.Testing;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Tests.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Review;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Tests that command output shows item names and paths with escape sequences, line separators, and bidirectional overrides replaced.</summary>
public sealed class PrintableOutputTests : IDisposable
{
    private const string ExportPassword = "fixture export password";
    private const string KeeperKey = "keeper|keeper.zip|rec-1";

    // An escape sequence, a C1 control sequence introducer, a line separator, and a right-to-left override.
    private const string Name = "Bank\u001b[2J\u009b31m\u2028\u202Eknab";
    private const string PrintableName = "Bank\uFFFD[2J\uFFFD31m \uFFFDknab";
    private static readonly char[] Unprintable = ['\u001b', '\u009b', '\u2028', '\u202E'];

    private readonly TemporaryDirectory temporaryDirectory = new();

    private string DirectoryPath => temporaryDirectory.Path;

    /// <inheritdoc />
    public void Dispose() => temporaryDirectory.Dispose();

    [Fact]
    public async Task Inspect_merge_and_the_merge_report_show_item_names_without_controls_line_separators_or_overrides()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLoginsAndMissingAttachment(DirectoryPath, Name);
        string report = Path.Combine(DirectoryPath, "merge.md");
        var ct = TestContext.Current.CancellationToken;

        // Act
        var inspect = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", .. inputs], ct);
        var merge = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", Path.Combine(DirectoryPath, "merged.json"), "--report", report], ct);
        string markdown = await File.ReadAllTextAsync(report, ct);

        // Assert
        inspect.ExitCode.ShouldBe(0);
        merge.ExitCode.ShouldBe(2);
        markdown.ShouldContainInOrder("## Decisions needed", "` (" + PrintableName + ")");
        string[] texts = [inspect.Output, merge.Error, markdown];
        texts.ShouldAllSatisfy(text =>
        {
            text.ShouldContain(PrintableName, StringComparison.Ordinal);
            Unprintable.ShouldAllSatisfy(character => text.ShouldNotContain(character));
        });
    }

    [Fact]
    public async Task Review_summary_shows_item_names_without_controls_line_separators_or_overrides()
    {
        // Arrange
        var terminal = new FakeTerminal
        {
            User = session =>
            {
                session.ChooseMember(session.GroupOf(KeeperKey).ShouldNotBeNull().Key, FieldPaths.LoginPassword, KeeperKey);
                session.Write(null);
                return 0;
            },
        };

        // Act
        var run = await ReviewRun.Invoke(new TestPrompts(ExportPassword).Environment(), terminal, [.. CliInputs.ConflictingLoginsAndMissingAttachment(DirectoryPath, Name), "-o", Path.Combine(DirectoryPath, "merged.json"), "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("  " + PrintableName + ": photo.png, not included in keeper.zip", StringComparison.Ordinal);
        string[] texts = [run.Output, run.Error];
        texts.ShouldAllSatisfy(text =>
        {
            text.ShouldContain(PrintableName, StringComparison.Ordinal);
            Unprintable.ShouldAllSatisfy(character => text.ShouldNotContain(character));
        });
    }

    [Fact]
    public async Task Review_warns_about_decisions_that_match_nothing_without_controls_line_separators_or_overrides()
    {
        // Arrange
        string[] inputs = CliInputs.NewerBitwardenLogin(DirectoryPath);
        string groupKey = "0000\u001b[2J\r\u009b\u2028\u202E0000";
        string decisionsPath = Path.Combine(DirectoryPath, "decisions.json");
        DecisionsFile.Write(decisionsPath, new MergeDecisions([new GroupDecision(groupKey, [FieldChoice.Empty(FieldPaths.Notes)])], MatchOverrides.None));

        // Act
        var run = await ReviewRun.Invoke(new TestPrompts(ExportPassword).Environment(), new FakeTerminal(), [.. inputs, "--decisions", decisionsPath], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Error.ShouldBe("Warning: decisions for group 0000\uFFFD[2J \uFFFD \uFFFD0000 match no merged group and are ignored.\n", StringComparer.Ordinal);
    }

    [Fact]
    public async Task Review_summary_shows_the_files_it_saved_and_wrote_without_controls_line_separators_or_overrides()
    {
        // Arrange
        string[] inputs = CliInputs.NewerBitwardenLogin(DirectoryPath);
        string decisionsPath = Path.Combine(DirectoryPath, "decisions\u009b\u2028\u202E.json");
        string output = Path.Combine(DirectoryPath, "merged\u009b\u2028\u202E.json");
        var terminal = new FakeTerminal
        {
            User = session =>
            {
                session.Save();
                session.Write(null);
                return 0;
            },
        };

        // Act
        var run = await ReviewRun.Invoke(new TestPrompts(ExportPassword).Environment(), terminal, [.. inputs, "--decisions", decisionsPath, "-o", output, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Saved the decisions to " + Path.Combine(DirectoryPath, "decisions\uFFFD \uFFFD.json") + ".\n", StringComparison.Ordinal);
        run.Output.ShouldContain("Wrote the merged vault to " + Path.Combine(DirectoryPath, "merged\uFFFD \uFFFD.json") + " as unencrypted JSON.", StringComparison.Ordinal);
        Unprintable.ShouldAllSatisfy(character => run.Output.ShouldNotContain(character));
    }
}
