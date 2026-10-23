using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

public sealed class VaultCommandLineTests
{
    private const string ExportPassword = "fixture export password";

    [Fact]
    public void Root_command_offers_inspect_scan_merge_and_review()
    {
        var command = VaultCommandLine.Create(new TestPrompts(ExportPassword).Environment());

        command.Subcommands.Select(subcommand => subcommand.Name).ShouldBe(["inspect", "scan", "merge", "review"]);
    }

    [Fact]
    public async Task Help_lists_the_commands_on_standard_output()
    {
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["--help"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        string[] commands = ["inspect", "scan", "merge", "review"];
        commands.ShouldAllSatisfy(command => run.Output.ShouldContain(command, StringComparison.Ordinal));
        run.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Merge_help_shows_every_option()
    {
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", "--help"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        string[] options = ["--output", "-o", "--decisions", "--policy <newest|ask|prefer:<input file name>>", "[default: newest]", "--plaintext", "--report", "--force"];
        options.ShouldAllSatisfy(option => run.Output.ShouldContain(option, StringComparison.Ordinal));
        run.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Version_writes_to_standard_output()
    {
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["--version"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldNotBeEmpty();
        run.Error.ShouldBeEmpty();
    }

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
    public async Task Invalid_command_lines_exit_1_with_the_error_on_standard_error(params string[] args)
    {
        var prompts = new TestPrompts(ExportPassword);
        var parseResult = VaultCommandLine.Parse(VaultCommandLine.Create(prompts.Environment()), args);

        var run = await CommandLineRun.Invoke(prompts.Environment(), args, TestContext.Current.CancellationToken);

        parseResult.Errors.ShouldNotBeEmpty();
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain(parseResult.Errors[0].Message, StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("newest")]
    [InlineData("ask")]
    [InlineData("prefer:keeper.json")]
    public void Policy_option_accepts_newest_ask_and_an_input_file_to_prefer(string policy)
    {
        var command = VaultCommandLine.Create(new TestPrompts(ExportPassword).Environment());

        var parseResult = VaultCommandLine.Parse(command, ["merge", "exports/keeper.json", "exports/bitwarden.json", "--output", "merged.json", "--policy", policy]);

        parseResult.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("inspect", "@keeper.json")]
    [InlineData("scan", "@keeper.json", "--report", "@scan.md")]
    [InlineData("merge", "@keeper.json", "--output", "@merged.json", "--decisions", "@decisions.json")]
    public void Arguments_starting_with_at_are_file_names_rather_than_response_files(params string[] args)
    {
        var command = VaultCommandLine.Create(new TestPrompts(ExportPassword).Environment());

        var parseResult = VaultCommandLine.Parse(command, args);

        parseResult.Errors.ShouldBeEmpty();
        parseResult.Tokens.Select(token => token.Value).ShouldBe(args);
    }

    [Fact]
    public void Policy_option_defaults_to_newest()
    {
        var command = VaultCommandLine.Create(new TestPrompts(ExportPassword).Environment());

        var parseResult = VaultCommandLine.Parse(command, ["merge", "keeper.json", "--output", "merged.json"]);

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("--policy").ShouldBe("newest");
    }
}
