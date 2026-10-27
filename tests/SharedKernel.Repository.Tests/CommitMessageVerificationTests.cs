namespace PandaBear.Repository.Tests;

/// <summary>Guards the <c>verify-commit-message</c> command for pull request events and commit message files.</summary>
public sealed class CommitMessageVerificationTests
{
    private const string TitleError = "PR title must be a Conventional Commit header of at most 100 characters.";
    private const string HeaderError = "Commit message must start with a Conventional Commit header of at most 100 characters.";
    private const string MissingHeaderError = "Commit message has no header.";
    private const string FooterError = "A breaking-change footer requires 'BREAKING CHANGE: <description>'.";
    private const string Scissors = "# ------------------------ >8 ------------------------";

    [Theory]
    [InlineData("feat: support the contract", "BREAKING CHANGE: remove an old field", 0)]
    [InlineData("feat(api)!: change booking contract", "", 0)]
    [InlineData("feat: change the contract", "BREAKING-CHANGE: remove the old field", 0)]
    [InlineData("Update contract", "", 2)]
    [InlineData("feat:    ", "", 2)]
    [InlineData("feat: first\nfix: injected", "", 2)]
    [InlineData("Merge branch 'feature' into main", "", 2)]
    [InlineData("fixup! feat: support the contract", "", 2)]
    [InlineData("feat: support the contract", "BREAKING CHANGE missing colon", 2)]
    [InlineData("feat: change the contract", "BREAKING CHANGE:", 2)]
    public async Task Event_rejects_invalid_squash_headers_and_breaking_footers(string title, string body, int expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        await PullRequestEvent.Write(Path.Combine(scope.Root, "event.json"), title, body, ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["verify-commit-message", "--event", "event.json"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(expected);
        result.Output.ShouldBe(string.Empty);
        processes.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(101, 2)]
    public async Task Event_title_is_limited_to_100_characters(int length, int expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        await PullRequestEvent.Write(Path.Combine(scope.Root, "event.json"), "feat: " + new string('x', length - "feat: ".Length), string.Empty, ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["verify-commit-message", "--event", "event.json"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Update contract", "", TitleError)]
    [InlineData("feat: support the contract", "BREAKING CHANGE missing colon", FooterError)]
    public async Task Event_errors_are_reported_on_standard_error_as_fixed_text(string title, string body, string expectedError)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var eventPath = Path.Combine(scope.Root, "event.json");
        await PullRequestEvent.Write(eventPath, title, body, ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(Path.GetDirectoryName(scope.Root)!, ["verify-commit-message", "--event", eventPath], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(expectedError + Environment.NewLine, "Errors never repeat the title or body, which the pull request author controls.");
    }

    [Theory]
    [InlineData("feat: support the contract")]
    [InlineData("feat(api)!: change the contract\n\nBREAKING CHANGE: remove an old field\n")]
    [InlineData("feat(Booking API): change the contract\n")]
    [InlineData("fix: preserve validation\r\n\r\nKeep the old rule.\r\nBREAKING-CHANGE: remove the old field\r\n")]
    [InlineData("feat: support the contract \t\n\nBREAKING CHANGE: remove an old field  \n")]
    [InlineData("\n \n# Please enter the commit message for your changes.\n  # An indented comment.\nfeat: support the contract\n")]
    [InlineData("feat: support the contract\n\n" + Scissors + "\n# Do not modify or remove the line above.\nBREAKING CHANGE missing colon\n")]
    [InlineData("Merge branch 'feature' into main\n\n# Conflicts:\n#\tsrc/Contract.cs\n")]
    [InlineData("Revert \"feat: support the contract\"\n\nThis reverts commit 0123456789abcdef0123456789abcdef01234567.\n")]
    [InlineData("Reapply \"feat: support the contract\"\n\nThis reverts commit 0123456789abcdef0123456789abcdef01234567.\n")]
    [InlineData("fixup! feat: support the contract\n")]
    [InlineData("squash! feat: support the contract\n\nAlso cover the contract.\n")]
    [InlineData("amend! feat: support the contract\n\nfeat: support the new contract\n")]
    [InlineData("fixup! amend! feat: support the contract\n")]
    [InlineData("fixup! Merge branch 'feature' into main\n")]
    public async Task Message_file_accepts_conventional_headers_and_the_messages_git_writes(string message)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "COMMIT_EDITMSG"), message, ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["verify-commit-message", "--file", "COMMIT_EDITMSG"], processes.Run, ct);

        // Assert
        result.Error.ShouldBe(string.Empty);
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe(string.Empty);
        processes.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", MissingHeaderError)]
    [InlineData("\n \n# Please enter the commit message for your changes.\n#\n", MissingHeaderError)]
    [InlineData(Scissors + "\n# Do not modify or remove the line above.\ndiff --git a/Contract.cs b/Contract.cs\n", MissingHeaderError)]
    [InlineData("Update contract\n", HeaderError)]
    [InlineData("feat:    \n", HeaderError)]
    [InlineData("  feat: support the contract\n", HeaderError)]
    [InlineData("\uFEFFfeat: support the contract\n", HeaderError)]
    [InlineData("fixup! Update contract\n", HeaderError)]
    [InlineData("fixup!feat: support the contract\n", HeaderError)]
    [InlineData("squash! feat: support the contract\n\nBREAKING CHANGE missing colon\n", FooterError)]
    [InlineData("feat: support the contract\n\nBREAKING CHANGE missing colon\n", FooterError)]
    [InlineData("feat: support the contract\n\nBREAKING-CHANGE:  \n", FooterError)]
    public async Task Message_file_without_a_valid_header_or_breaking_footer_fails_with_fixed_text(string message, string expectedError)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "COMMIT_EDITMSG"), message, ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["verify-commit-message", "--file", "COMMIT_EDITMSG"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(expectedError + Environment.NewLine, "Errors never repeat the message, which the commit author controls.");
        result.Output.ShouldBe(string.Empty);
        processes.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("feat: ", 100, 0)]
    [InlineData("feat: ", 101, 2)]
    [InlineData("Merge ", 101, 0)]
    [InlineData("fixup! feat: ", 107, 0)]
    [InlineData("fixup! feat: ", 108, 2)]
    public async Task Message_file_header_is_limited_to_100_characters_unless_git_wrote_it(string prefix, int length, int expected)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(prefix);
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "COMMIT_EDITMSG"), prefix + new string('x', length - prefix.Length) + "\n", ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["verify-commit-message", "--file", "COMMIT_EDITMSG"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(expected);
    }

    [Theory]
    [InlineData("--event", "missing.json")]
    [InlineData("--file", "MISSING_EDITMSG")]
    public async Task Missing_input_file_fails_with_the_domain_exit_code(string option, string path)
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["verify-commit-message", option, path], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(path, StringComparison.Ordinal);
    }
}
