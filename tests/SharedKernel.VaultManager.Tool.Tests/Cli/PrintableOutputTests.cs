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

    private readonly string directory = Directory.CreateTempSubdirectory("vault-cli-printable-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    /// <summary>Inspect output, the merge error output, and the merge report show an item name containing an escape, a C1 introducer, a line separator, and an override as its printable form, with exit codes 0 and 2.</summary>
    [Fact]
    public async Task Inspect_merge_and_the_merge_report_show_item_names_without_controls_line_separators_or_overrides()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLoginsAndMissingAttachment(directory, Name);
        string report = Path.Combine(directory, "merge.md");
        var ct = TestContext.Current.CancellationToken;

        // Act
        var inspect = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", .. inputs], ct);
        var merge = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", Path.Combine(directory, "merged.json"), "--report", report], ct);
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

    /// <summary>The review summary lists the attachment-less item under its printable name and neither output stream contains any unprintable character.</summary>
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
        var run = await ReviewRun.Invoke(new TestPrompts(ExportPassword).Environment(), terminal, [.. CliInputs.ConflictingLoginsAndMissingAttachment(directory, Name), "-o", Path.Combine(directory, "merged.json"), "--plaintext"], TestContext.Current.CancellationToken);

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

    /// <summary>A decisions file whose group key matches no merged group yields exit code 0 and a warning that prints the key with its unprintable characters replaced.</summary>
    [Fact]
    public async Task Review_warns_about_decisions_that_match_nothing_without_controls_line_separators_or_overrides()
    {
        // Arrange
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string groupKey = "0000\u001b[2J\r\u009b\u2028\u202E0000";
        string decisionsPath = Path.Combine(directory, "decisions.json");
        DecisionsFile.Write(decisionsPath, new MergeDecisions([new GroupDecision(groupKey, [FieldChoice.Empty(FieldPaths.Notes)])], MatchOverrides.None));

        // Act
        var run = await ReviewRun.Invoke(new TestPrompts(ExportPassword).Environment(), new FakeTerminal(), [.. inputs, "--decisions", decisionsPath], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Error.ShouldBe("Warning: decisions for group 0000\uFFFD[2J \uFFFD \uFFFD0000 match no merged group and are ignored.\n", StringComparer.Ordinal);
    }

    /// <summary>Saving decisions and writing the merged vault to paths containing unprintable characters prints both paths in printable form and leaves no unprintable character in the output.</summary>
    [Fact]
    public async Task Review_summary_shows_the_files_it_saved_and_wrote_without_controls_line_separators_or_overrides()
    {
        // Arrange
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string decisionsPath = Path.Combine(directory, "decisions\u009b\u2028\u202E.json");
        string output = Path.Combine(directory, "merged\u009b\u2028\u202E.json");
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
        run.Output.ShouldContain("Saved the decisions to " + Path.Combine(directory, "decisions\uFFFD \uFFFD.json") + ".\n", StringComparison.Ordinal);
        run.Output.ShouldContain("Wrote the merged vault to " + Path.Combine(directory, "merged\uFFFD \uFFFD.json") + " as unencrypted JSON.", StringComparison.Ordinal);
        Unprintable.ShouldAllSatisfy(character => run.Output.ShouldNotContain(character));
    }
}
