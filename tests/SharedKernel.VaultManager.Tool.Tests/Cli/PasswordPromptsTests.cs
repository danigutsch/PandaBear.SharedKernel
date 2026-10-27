using System.CommandLine;
using PandaBear.VaultManager.Tool.Cli;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Tests the text of the password prompts.</summary>
public sealed class PasswordPromptsTests
{
    private const string ExportPassword = "fixture export password";
    private const string FilePassword = "fixture file password";

    /// <summary>Verifies that prompts replace control characters in their text.</summary>
    [Fact]
    public async Task Prompts_replace_control_characters_in_their_text()
    {
        // Arrange
        var prompts = new TestPrompts(ExportPassword, FilePassword);
        string prompt = string.Concat("Password for a", (char)0x1B, "[2J", (char)0x9B, "b.json");
        RootCommand command = ActionCommands.Running(prompts.Environment(), run =>
        {
            _ = run.Environment.ReadPassword(prompt);
            _ = run.Environment.ReadNewPassword(prompt);
            return ExitCodes.Success;
        });

        // Act
        var result = await CommandLineRun.Invoke(command, [], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        prompts.FilePasswordPrompts.ShouldBe(["Password for a\uFFFD[2J\uFFFDb.json"]);
        prompts.NewPasswordPrompts.ShouldBe(["Password for a\uFFFD[2J\uFFFDb.json"]);
    }
}
