using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Tests the vault command tree: its subcommands, help and version output, argument validation, and printable error text.</summary>
public sealed class VaultCommandLineTests
{
    private const string ExportPassword = "fixture export password";

    /// <summary>The root command has exactly the subcommands inspect, scan, merge, review, catalog, and apply, in that order.</summary>
    [Fact]
    public void Root_command_offers_inspect_scan_merge_review_catalog_and_apply()
    {
        // Act
        var command = VaultCommandLine.Create(new TestPrompts(ExportPassword).Environment());

        // Assert
        command.Subcommands.Select(subcommand => subcommand.Name).ShouldBe(["inspect", "scan", "merge", "review", "catalog", "apply"]);
    }

    /// <summary>Running with --help exits with code 0, prints inspect, scan, merge, and review on standard output, and prints nothing to standard error.</summary>
    [Fact]
    public async Task Help_lists_the_commands_on_standard_output()
    {
        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["--help"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        string[] commands = ["inspect", "scan", "merge", "review"];
        commands.ShouldAllSatisfy(command => run.Output.ShouldContain(command, StringComparison.Ordinal));
        run.Error.ShouldBeEmpty();
    }

    /// <summary>The merge command's help lists the output, decisions, policy (with its newest default), plaintext, report, and force options.</summary>
    [Fact]
    public async Task Merge_help_shows_every_option()
    {
        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", "--help"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        string[] options = ["--output", "-o", "--decisions", "--policy <newest|ask|prefer:<input file name>>", "[default: newest]", "--plaintext", "--report", "--force"];
        options.ShouldAllSatisfy(option => run.Output.ShouldContain(option, StringComparison.Ordinal));
        run.Error.ShouldBeEmpty();
    }

    /// <summary>Running with --version exits with code 0 and prints the version to standard output, not standard error.</summary>
    [Fact]
    public async Task Version_writes_to_standard_output()
    {
        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["--version"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldNotBeEmpty();
        run.Error.ShouldBeEmpty();
    }

    /// <summary>Command lines with missing, empty, or blank arguments or an unsupported policy exit with code 1, print the first parse error to standard error, and ask for no password.</summary>
    /// <param name="args">The command-line arguments that fail to parse.</param>
    [Theory]
    [InlineData]
    [InlineData("review")]
    [InlineData("show", "keeper.json")]
    [InlineData("inspect")]
    [InlineData("scan")]
    [InlineData("scan", "keeper.json", "--report")]
    [InlineData("merge", "keeper.json")]
    [InlineData("merge", "keeper.json", "--output")]
    [InlineData("merge", "keeper.json", "--output", "merged.json", "--policy", "oldest")]
    [InlineData("merge", "keeper.json", "--output", "merged.json", "--policy", "prefer:")]
    [InlineData("merge", "keeper.json", "--output", "merged.json", "--policy", "prefer:bitwarden.json")]
    [InlineData("merge", "keeper/export.json", "bitwarden/export.json", "--output", "merged.json", "--policy", "prefer:export.json")]
    [InlineData("inspect", "")]
    [InlineData("inspect", "keeper.json", " ")]
    [InlineData("scan", "")]
    [InlineData("scan", "keeper.json", "--report", "")]
    [InlineData("scan", "keeper.json", "--report", "  ")]
    [InlineData("merge", "", "--output", "merged.json")]
    [InlineData("merge", "keeper.json", "--output", "")]
    [InlineData("merge", "keeper.json", "--output", " ")]
    [InlineData("merge", "keeper.json", "--output", "merged.json", "--decisions", "")]
    [InlineData("merge", "keeper.json", "--output", "merged.json", "--report", " ")]
    [InlineData("review", "keeper.json", "--decisions", "")]
    [InlineData("review", "keeper.json", "--decisions", "  ")]
    [InlineData("review", "keeper.json", "--output", "")]
    [InlineData("review", "keeper.json", "--output", "  ")]
    public async Task Invalid_command_lines_exit_1_with_the_error_on_standard_error(params string[] args)
    {
        // Arrange
        var prompts = new TestPrompts(ExportPassword);
        var parseResult = VaultCommandLine.Parse(VaultCommandLine.Create(prompts.Environment()), args);

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), args, TestContext.Current.CancellationToken);

        // Assert
        parseResult.Errors.ShouldNotBeEmpty();
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain(parseResult.Errors[0].Message, StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    /// <summary>An unrecognized argument containing escape, C1, and right-to-left override characters exits with code 1 and is echoed in the error with each replaced by U+FFFD.</summary>
    [Fact]
    public async Task Parse_errors_print_an_argument_without_its_escape_and_bidirectional_characters()
    {
        // Arrange
        string[] unprintable = ["\u001B", "\u009B", "\u202E"];

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["x\u001B[2Jy\u009B31m\u202Ez"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("Unrecognized command or argument 'x\uFFFD[2Jy\uFFFD31m\uFFFDz'.", StringComparison.Ordinal);
        run.Output.ShouldContain("Usage:", StringComparison.Ordinal);
        string printed = run.Output + run.Error;
        unprintable.ShouldAllSatisfy(character => printed.ShouldNotContain(character, StringComparison.Ordinal));
    }

    /// <summary>A mistyped command containing an escape character exits with code 1 and the did-you-mean message shows it with U+FFFD instead of the escape.</summary>
    [Fact]
    public async Task Typo_corrections_print_an_argument_without_its_escape_characters()
    {
        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["revi\u001Bw"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain("'revi\uFFFDw' was not matched. Did you mean one of the following?", StringComparison.Ordinal);
        string printed = run.Output + run.Error;
        printed.ShouldNotContain("\u001B", StringComparison.Ordinal);
    }

    /// <summary>The merge command parses without errors when the policy is newest, ask, or prefer followed by an input file name.</summary>
    /// <param name="policy">The policy option value.</param>
    [Theory]
    [InlineData("newest")]
    [InlineData("ask")]
    [InlineData("prefer:keeper.json")]
    public void Policy_option_accepts_newest_ask_and_an_input_file_to_prefer(string policy)
    {
        // Arrange
        var command = VaultCommandLine.Create(new TestPrompts(ExportPassword).Environment());

        // Act
        var parseResult = VaultCommandLine.Parse(command, ["merge", "exports/keeper.json", "exports/bitwarden.json", "--output", "merged.json", "--policy", policy]);

        // Assert
        parseResult.Errors.ShouldBeEmpty();
    }

    /// <summary>Arguments that start with an at sign parse without errors and stay in the token list as literal values instead of being expanded as response files.</summary>
    /// <param name="args">The command-line arguments, some starting with an at sign.</param>
    [Theory]
    [InlineData("inspect", "@keeper.json")]
    [InlineData("scan", "@keeper.json", "--report", "@scan.md")]
    [InlineData("merge", "@keeper.json", "--output", "@merged.json", "--decisions", "@decisions.json")]
    public void Arguments_starting_with_at_are_file_names_rather_than_response_files(params string[] args)
    {
        // Arrange
        var command = VaultCommandLine.Create(new TestPrompts(ExportPassword).Environment());

        // Act
        var parseResult = VaultCommandLine.Parse(command, args);

        // Assert
        parseResult.Errors.ShouldBeEmpty();
        parseResult.Tokens.Select(token => token.Value).ShouldBe(args);
    }

    /// <summary>A merge command line without the policy option parses without errors and the policy value is newest.</summary>
    [Fact]
    public void Policy_option_defaults_to_newest()
    {
        // Arrange
        var command = VaultCommandLine.Create(new TestPrompts(ExportPassword).Environment());

        // Act
        var parseResult = VaultCommandLine.Parse(command, ["merge", "keeper.json", "--output", "merged.json"]);

        // Assert
        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("--policy").ShouldBe("newest");
    }
}
