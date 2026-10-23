using System.CommandLine;
using PandaBear.VaultManager.Tool.Cli;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

public sealed class PasswordPromptsTests
{
    private const string ExportPassword = "fixture export password";
    private const string FilePassword = "fixture file password";

    [Fact]
    public async Task Prompts_show_control_characters_in_their_text_as_spaces()
    {
        var prompts = new TestPrompts(ExportPassword, FilePassword);
        string prompt = string.Concat("Password for a", (char)0x1B, "[2J", (char)0x9B, "b.json");
        RootCommand command = ActionCommands.Running(prompts.Environment(), run =>
        {
            _ = run.Environment.ReadPassword(prompt);
            _ = run.Environment.ReadNewPassword(prompt);
            return ExitCodes.Success;
        });

        var result = await CommandLineRun.Invoke(command, [], TestContext.Current.CancellationToken);

        result.ExitCode.ShouldBe(0);
        prompts.FilePasswordPrompts.ShouldBe(["Password for a [2J b.json"]);
        prompts.NewPasswordPrompts.ShouldBe(["Password for a [2J b.json"]);
    }
}
