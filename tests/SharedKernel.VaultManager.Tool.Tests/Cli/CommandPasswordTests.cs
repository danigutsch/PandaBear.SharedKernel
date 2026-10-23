using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

public sealed class CommandPasswordTests : IDisposable
{
    private const string ExportPassword = "fixture export password";
    private const string FilePassword = "fixture file password";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-cli-passwords-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task Merge_without_a_terminal_refuses_an_encrypted_output_before_any_work()
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        string report = Path.Combine(directory, "report.md");
        var prompts = new TestPrompts(ExportPassword) { IsInteractive = false };

        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", .. inputs, "--output", output, "--report", report], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldContain("standard input is not a terminal", StringComparison.Ordinal);
        run.Error.ShouldContain("--plaintext", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
        File.Exists(report).ShouldBeFalse();
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Merge_without_a_terminal_writes_a_plaintext_output()
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword) { IsInteractive = false }.Environment(), ["merge", .. inputs, "--output", output, "--plaintext"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        FormatDetector.Detect(output).ShouldBe(VaultFileFormat.BitwardenJson);
    }

    [Theory]
    [InlineData("inspect")]
    [InlineData("scan")]
    [InlineData("merge")]
    public async Task Commands_without_a_terminal_refuse_a_protected_input_by_name_before_any_work(string command)
    {
        string[] inputs = [CliInputs.NewerBitwardenLogin(directory)[0], CliInputs.ProtectedBitwarden(directory, FilePassword)];
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword, FilePassword) { IsInteractive = false };
        string[] args = command == "merge" ? ["merge", .. inputs, "--output", output, "--plaintext"] : [command, .. inputs];

        var run = await CommandLineRun.Invoke(prompts.Environment(), args, TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldStartWith("protected.json is password-protected, and standard input is not a terminal", StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Merge_asks_once_for_the_password_of_a_protected_input()
    {
        string input = CliInputs.ProtectedBitwarden(directory, FilePassword);
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword, FilePassword);

        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", input, "--output", output, "--plaintext"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(0);
        prompts.FilePasswordPrompts.ShouldBe(["Password for protected.json"]);
        File.Exists(output).ShouldBeTrue();
    }

    [Fact]
    public async Task Wrong_input_password_exits_3()
    {
        string input = CliInputs.ProtectedBitwarden(directory, FilePassword);
        string output = Path.Combine(directory, "merged.json");

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword, "fixture wrong password").Environment(), ["merge", input, "--output", output, "--plaintext"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("The password is wrong", StringComparison.Ordinal);
        run.Error.ShouldNotContain("fixture wrong password", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Password_prompt_that_fails_exits_3_with_its_message()
    {
        string input = CliInputs.ProtectedBitwarden(directory, FilePassword);

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", input], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("The test inputs are not password-protected.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Merge_canceled_at_the_password_prompt_exits_1_and_writes_nothing()
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var prompts = new TestPrompts(ExportPassword) { OnNewPassword = cancellation.Cancel };

        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", .. inputs, "--output", output], cancellation.Token);

        run.ExitCode.ShouldBe(1);
        prompts.NewPasswordPrompts.ShouldHaveSingleItem();
        File.Exists(output).ShouldBeFalse();
        Directory.GetFiles(directory).Length.ShouldBe(2);
    }

    [Theory]
    [InlineData("inspect")]
    [InlineData("scan")]
    [InlineData("merge")]
    public async Task Commands_canceled_before_they_start_exit_1(string command)
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        string[] args = command == "merge" ? ["merge", .. inputs, "--output", output] : [command, .. inputs];

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, new CancellationToken(canceled: true));

        run.ExitCode.ShouldBe(1);
        run.Output.ShouldBeEmpty();
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Unexpected_error_exits_1_without_showing_its_message()
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword) { OnNewPassword = () => throw new FormatException("fixture-value-in-a-message") };

        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", .. inputs, "--output", output], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("System.FormatException", StringComparison.Ordinal);
        run.Error.ShouldNotContain("fixture-value-in-a-message", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }
}
