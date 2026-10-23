using System.Globalization;
using System.Text.Json.Nodes;
using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Review;
using PandaBear.VaultManager.Tool.Secrets;
using PandaBear.VaultManager.Tool.Tests.Cli;
using PandaBear.VaultManager.Tool.Tests.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Review;

public sealed class ReviewCommandTests : IDisposable
{
    private const string ExportPassword = "fixture export password";
    private const string FilePassword = "fixture file password";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-review-command-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task Review_without_a_terminal_exits_3_and_points_to_merge_with_a_decisions_file()
    {
        // Arrange
        string[] inputs = [CliInputs.ConflictingLogins(directory)[0], CliInputs.ProtectedBitwarden(directory, FilePassword)];
        var prompts = new TestPrompts(ExportPassword, FilePassword) { IsInteractive = false };

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), ["review", .. inputs], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldContain("needs a terminal", StringComparison.Ordinal);
        run.Error.ShouldContain("merge", StringComparison.Ordinal);
        run.Error.ShouldContain("--decisions <file>", StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Review_with_redirected_output_exits_3_before_reading_any_input()
    {
        // Arrange
        var prompts = new TestPrompts(ExportPassword, FilePassword);
        var terminal = new FakeTerminal { OutputIsTerminal = false };

        // Act
        var run = await ReviewRun.Invoke(prompts.Environment(), terminal, [CliInputs.ProtectedBitwarden(directory, FilePassword)], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("--decisions <file>", StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
        terminal.Sessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Help_lists_review_and_review_help_shows_every_option()
    {
        // Arrange
        var environment = new TestPrompts(ExportPassword).Environment();

        // Act
        var root = await CommandLineRun.Invoke(environment, ["--help"], TestContext.Current.CancellationToken);
        var review = await CommandLineRun.Invoke(environment, ["review", "--help"], TestContext.Current.CancellationToken);

        // Assert
        root.ExitCode.ShouldBe(0);
        root.Output.ShouldContain("review", StringComparison.Ordinal);
        review.ExitCode.ShouldBe(0);
        string[] options = ["<inputs>...", "--decisions <file>", "-o, --output <file>", "--plaintext", "--force"];
        options.ShouldAllSatisfy(option => review.Output.ShouldContain(option, StringComparison.Ordinal));
        review.Error.ShouldBeEmpty();
    }

    [Fact]
    public void Tool_readme_shows_review_and_its_command_lines_parse_with_the_real_tree()
    {
        // Arrange
        var command = VaultCommandLine.Create(new TestPrompts(ExportPassword).Environment());

        // Act
        string[][] reviews = [.. ToolReadme.CommandLines().Where(args => args.Length > 0 && string.Equals(args[0], "review", StringComparison.Ordinal))];

        // Assert
        reviews.ShouldNotBeEmpty();
        reviews.ShouldAllSatisfy(args => command.Parse(args).Errors.ShouldBeEmpty());
    }

    [Fact]
    public void Tool_readme_lists_every_review_key()
    {
        // Act
        string readme = ReviewReadme.Text();

        // Assert
        ReviewKeys.All.ShouldAllSatisfy(key => readme.ShouldContain("`" + key.Key + "`", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("--plaintext")]
    [InlineData("--force")]
    public async Task Plaintext_or_force_without_an_output_is_an_invalid_command_line(string option)
    {
        // Arrange
        var terminal = new FakeTerminal();

        // Act
        var run = await ReviewRun.Invoke(new TestPrompts(ExportPassword).Environment(), terminal, [.. CliInputs.ConflictingLogins(directory), option], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("--output", StringComparison.Ordinal);
        terminal.Sessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Review_refuses_an_output_that_is_an_input_before_asking_for_passwords()
    {
        // Arrange
        string input = CliInputs.ProtectedBitwarden(directory, FilePassword);
        var prompts = new TestPrompts(ExportPassword, FilePassword);
        var terminal = new FakeTerminal();

        // Act
        var run = await ReviewRun.Invoke(prompts.Environment(), terminal, [input, "--output", input, "--force"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("may be the same file as an input file", StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
        terminal.Sessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Review_refuses_an_existing_output_without_force_before_asking_for_passwords()
    {
        // Arrange
        string input = CliInputs.ProtectedBitwarden(directory, FilePassword);
        string output = Path.Combine(directory, "merged.json");
        await File.WriteAllTextAsync(output, "fixture existing output", TestContext.Current.CancellationToken);
        var prompts = new TestPrompts(ExportPassword, FilePassword);
        var terminal = new FakeTerminal();

        // Act
        var run = await ReviewRun.Invoke(prompts.Environment(), terminal, [input, "--output", output], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("--force", StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
        terminal.Sessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Review_names_an_unreadable_input_before_asking_for_passwords()
    {
        // Arrange
        var prompts = new TestPrompts(ExportPassword, FilePassword);
        var terminal = new FakeTerminal();

        // Act
        var run = await ReviewRun.Invoke(prompts.Environment(), terminal, [Path.Combine(directory, "missing.json"), CliInputs.ProtectedBitwarden(directory, FilePassword)], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldStartWith("missing.json: ", StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
        terminal.Sessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Review_asks_input_passwords_on_the_console_before_the_ui_starts()
    {
        // Arrange
        var prompts = new TestPrompts(ExportPassword, FilePassword);
        var terminal = new FakeTerminal { Prompts = () => prompts.FilePasswordPrompts.Count };

        // Act
        var run = await ReviewRun.Invoke(prompts.Environment(), terminal, [CliInputs.ProtectedBitwarden(directory, FilePassword)], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        terminal.PromptsBeforeRun.ShouldBe([1]);
        prompts.FilePasswordPrompts.ShouldBe(["Password for protected.json"]);
        prompts.NewPasswordPrompts.ShouldBeEmpty();
        terminal.Sessions.ShouldHaveSingleItem().Result.Merged.Items.ShouldHaveSingleItem().Name.ShouldBe("Example");
    }

    [Fact]
    public async Task Wrong_input_password_exits_3_without_starting_the_ui()
    {
        // Arrange
        var terminal = new FakeTerminal();

        // Act
        var run = await ReviewRun.Invoke(new TestPrompts(ExportPassword, "fixture wrong password").Environment(), terminal, [CliInputs.ProtectedBitwarden(directory, FilePassword)], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("The password is wrong", StringComparison.Ordinal);
        run.Error.ShouldNotContain("fixture wrong password", StringComparison.Ordinal);
        terminal.Sessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Review_saves_to_the_decisions_file_and_starts_from_it_the_next_time()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string decisionsPath = Path.Combine(directory, "decisions.json");
        var environment = new TestPrompts(ExportPassword).Environment();
        var first = new FakeTerminal
        {
            User = session =>
            {
                session.ChooseMember(session.Groups(GroupFilter.All, null)[0].Key, FieldPaths.LoginPassword, CliInputs.KeeperKey);
                session.Save();
                return 0;
            },
        };
        var second = new FakeTerminal();

        // Act
        var run = await ReviewRun.Invoke(environment, first, [.. inputs, "--decisions", decisionsPath], TestContext.Current.CancellationToken);
        await ReviewRun.Invoke(environment, second, [.. inputs, "--decisions", decisionsPath], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Saved the decisions to " + decisionsPath, StringComparison.Ordinal);
        var reopened = second.Sessions.ShouldHaveSingleItem();
        reopened.Pending.IsEmpty.ShouldBeTrue();
        reopened.Decisions.Groups.ShouldHaveSingleItem().Choices.ShouldBe([FieldChoice.Member(FieldPaths.LoginPassword, CliInputs.KeeperKey)]);
    }

    [Fact]
    public async Task Review_writes_the_merged_vault_to_the_output_and_says_so()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string output = Path.Combine(directory, "merged.json");
        var terminal = new FakeTerminal
        {
            User = session =>
            {
                session.ChooseMember(session.Groups(GroupFilter.All, null)[0].Key, FieldPaths.LoginPassword, CliInputs.BitwardenKey);
                session.Write(null);
                return 0;
            },
        };

        // Act
        var run = await ReviewRun.Invoke(new TestPrompts(ExportPassword).Environment(), terminal, [.. inputs, "-o", output, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Wrote the merged vault to " + output + " as unencrypted JSON.", StringComparison.Ordinal);
        run.Output.ShouldNotContain(CliInputs.BitwardenPassword, StringComparison.Ordinal);
        FormatDetector.Detect(output).ShouldBe(VaultFileFormat.BitwardenJson);
    }

    [Fact]
    public async Task Review_counts_the_attachments_to_upload_and_lists_them_once_it_wrote_the_merged_vault()
    {
        // Arrange
        string input = CliInputs.KeeperZipWithFolderAndAttachments(directory);
        string output = Path.Combine(directory, "merged.json");
        var environment = new TestPrompts(ExportPassword).Environment();
        var writes = new FakeTerminal
        {
            User = session =>
            {
                session.Write(null);
                return 0;
            },
        };

        // Act
        var quit = await ReviewRun.Invoke(environment, new FakeTerminal(), [input, "-o", output, "--plaintext"], TestContext.Current.CancellationToken);
        var written = await ReviewRun.Invoke(environment, writes, [input, "-o", output, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        quit.ExitCode.ShouldBe(0);
        quit.Output.ShouldEndWith("Attachments to upload after the import: 2\n", StringComparison.Ordinal);
        written.ExitCode.ShouldBe(0);
        written.Output.ShouldEndWith(
            """
            Attachments to upload after the import: 2
              Backup: codes.txt, in keeper.zip at files/file-codes
              Backup: photo.png, not included in keeper.zip; download it from the source vault

            """.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_counts_and_lists_the_files_of_attachment_folders_that_match_no_single_item()
    {
        // Arrange
        string zip = PipelineInputs.Write(directory, "bitwarden.zip", PipelineInputs.TwoBanksZip());
        string output = Path.Combine(directory, "merged.json");
        var terminal = new FakeTerminal
        {
            User = session =>
            {
                session.Write(null);
                return 0;
            },
        };

        // Act
        var run = await ReviewRun.Invoke(new TestPrompts(ExportPassword).Environment(), terminal, [zip, "-o", output, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldEndWith(
            """
            Attachments to upload after the import: 2
              statement.txt, in bitwarden.zip at attachments/Bank/statement.txt; its folder matches 2 items, so upload it to the one it belongs to
              contract.txt, in bitwarden.zip at attachments/Bank_1/contract.txt; its folder matches 2 items, so upload it to the one it belongs to

            """.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_prints_the_write_warnings_and_placement_problems_of_the_written_vault()
    {
        // Arrange
        string huge = TestSecrets.FromAlphabet(TestSecrets.Base64, 4000, 640);
        JsonObject withHistory = PipelineInputs.BitwardenLogin("bw-1", "Example", "alice", CliInputs.BitwardenPassword, "https://example.com");
        withHistory["passwordHistory"] = new JsonArray(
        [
            .. Enumerable.Range(1, 6).Select(month => (JsonNode)new JsonObject
            {
                ["lastUsedDate"] = string.Create(CultureInfo.InvariantCulture, $"2025-0{month}-01T00:00:00Z"),
                ["password"] = "fixture-old-password-" + month,
            }),
        ]);
        string[] inputs =
        [
            PipelineInputs.Write(directory, "keeper.json", PipelineInputs.KeeperJson(PipelineInputs.KeeperLogin("rec-long", "Long", "bob", url: "https://long.example.com", notes: "Password: " + huge))),
            PipelineInputs.Write(directory, "bitwarden.json", PipelineInputs.BitwardenJson(withHistory)),
        ];
        string output = Path.Combine(directory, "merged.json");
        string? accepted = null;
        var terminal = new FakeTerminal
        {
            User = session =>
            {
                ReviewGroup group = session.GroupOf("keeper|keeper.json|rec-long").ShouldNotBeNull();
                foreach (ReviewFinding finding in session.Findings(group.Key).Where(finding => finding.IsPending))
                {
                    bool password = finding.Kind == SecretKind.Password;
                    accepted = password ? finding.FindingId : accepted;
                    session.DecideFinding(finding.OriginKey, finding.FindingId, password ? FindingDecision.Accepted : FindingDecision.Rejected);
                }

                session.Write(null);
                return 0;
            },
        };

        // Act
        var run = await ReviewRun.Invoke(new TestPrompts(ExportPassword).Environment(), terminal, [.. inputs, "-o", output, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Error.ShouldContain("Warning: \"Example\": password history beyond 5 entries moved to hidden fields.\n", StringComparison.Ordinal);
        run.Error.ShouldContain("Warning: \"Long\": " + accepted.ShouldNotBeNull() + ": value exceeds the Bitwarden field budget; left in place\n", StringComparison.Ordinal);
        run.Error.ShouldNotContain(huge, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_canceled_before_it_starts_exits_1_without_starting_the_ui()
    {
        // Arrange
        var terminal = new FakeTerminal();

        // Act
        var run = await ReviewRun.Invoke(new TestPrompts(ExportPassword).Environment(), terminal, CliInputs.ConflictingLogins(directory), new CancellationToken(canceled: true));

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldBeEmpty();
        terminal.Sessions.ShouldBeEmpty();
    }
}
