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
        // Arrange
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        string report = Path.Combine(directory, "report.md");
        var prompts = new TestPrompts(ExportPassword) { IsInteractive = false };

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", .. inputs, "--output", output, "--report", report], TestContext.Current.CancellationToken);

        // Assert
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
        // Arrange
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword) { IsInteractive = false }.Environment(), ["merge", .. inputs, "--output", output, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        FormatDetector.Detect(output).ShouldBe(VaultFileFormat.BitwardenJson);
    }

    [Theory]
    [InlineData("inspect")]
    [InlineData("scan")]
    [InlineData("merge")]
    public async Task Commands_without_a_terminal_refuse_a_protected_input_by_name_before_any_work(string command)
    {
        // Arrange
        string[] inputs = [CliInputs.NewerBitwardenLogin(directory)[0], CliInputs.ProtectedBitwarden(directory, FilePassword)];
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword, FilePassword) { IsInteractive = false };
        string[] args = command == "merge" ? ["merge", .. inputs, "--output", output, "--plaintext"] : [command, .. inputs];

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), args, TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldStartWith("protected.json is password-protected, and standard input is not a terminal", StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
        File.Exists(output).ShouldBeFalse();
    }

    [Theory]
    [InlineData("inspect", "kdfIterations", 1000, "PBKDF2 iterations must be between")]
    [InlineData("scan", "kdfType", 7, "Unsupported key derivation type")]
    [InlineData("merge", "kdfIterations", 3_000_000, "PBKDF2 iterations must be between")]
    public async Task Commands_refuse_a_protected_input_with_unsupported_key_derivation_before_asking_its_password(string command, string property, int value, string message)
    {
        // Arrange
        string input = CliInputs.ProtectedBitwarden(directory, FilePassword);
        string text = await File.ReadAllTextAsync(input, TestContext.Current.CancellationToken);
        var envelope = System.Text.Json.Nodes.JsonNode.Parse(text).ShouldNotBeNull().AsObject();
        envelope[property] = value;
        await File.WriteAllTextAsync(input, envelope.ToJsonString(), TestContext.Current.CancellationToken);
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword, FilePassword);
        string[] args = command == "merge" ? ["merge", input, "--output", output, "--plaintext"] : [command, input];

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), args, TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldStartWith("protected.json: " + message, StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Merge_asks_once_for_the_password_of_a_protected_input()
    {
        // Arrange
        string input = CliInputs.ProtectedBitwarden(directory, FilePassword);
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword, FilePassword);

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", input, "--output", output, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        prompts.FilePasswordPrompts.ShouldBe(["Password for protected.json"]);
        File.Exists(output).ShouldBeTrue();
    }

    [Fact]
    public async Task Merge_reads_each_input_once_and_asks_its_password_once()
    {
        // Arrange
        string input = CliInputs.ProtectedBitwarden(directory, FilePassword);
        string output = Path.Combine(directory, "merged.json");

        // The input is gone once its password is typed, so a merge that read it again would fail.
        var prompts = new TestPrompts(ExportPassword, FilePassword) { OnFilePassword = () => File.Delete(input) };

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", input, "--output", output, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Error.ShouldBeEmpty();
        prompts.FilePasswordPrompts.ShouldBe(["Password for protected.json"]);
        FormatDetector.Detect(output).ShouldBe(VaultFileFormat.BitwardenJson);
    }

    [Fact]
    public async Task Wrong_input_password_exits_3()
    {
        // Arrange
        string input = CliInputs.ProtectedBitwarden(directory, FilePassword);
        string output = Path.Combine(directory, "merged.json");

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword, "fixture wrong password").Environment(), ["merge", input, "--output", output, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("The password is wrong", StringComparison.Ordinal);
        run.Error.ShouldNotContain("fixture wrong password", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Password_prompt_that_fails_exits_3_with_its_message()
    {
        // Arrange
        string input = CliInputs.ProtectedBitwarden(directory, FilePassword);

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", input], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("The test inputs are not password-protected.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Merge_canceled_at_the_password_prompt_exits_1_and_writes_nothing()
    {
        // Arrange
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var prompts = new TestPrompts(ExportPassword) { OnNewPassword = cancellation.Cancel };

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", .. inputs, "--output", output], cancellation.Token);

        // Assert
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
        // Arrange
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        string[] args = command == "merge" ? ["merge", .. inputs, "--output", output] : [command, .. inputs];

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, new CancellationToken(canceled: true));

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Output.ShouldBeEmpty();
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Unexpected_error_exits_1_without_showing_its_message()
    {
        // Arrange
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword) { OnNewPassword = () => throw new FormatException("fixture-value-in-a-message") };

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", .. inputs, "--output", output], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("System.FormatException", StringComparison.Ordinal);
        run.Error.ShouldNotContain("fixture-value-in-a-message", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }
}
