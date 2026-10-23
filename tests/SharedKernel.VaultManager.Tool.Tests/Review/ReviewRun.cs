using System.CommandLine;
using System.CommandLine.Help;
using System.Globalization;
using PandaBear.VaultManager.Tool.Cli;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Cli;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>Invokes the review command with a fake terminal, capturing every output like the real command tree tests do.</summary>
internal static class ReviewRun
{
    public static async Task<CommandLineRun> Invoke(VaultCommandEnvironment environment, FakeTerminal terminal, IReadOnlyList<string> args, CancellationToken ct)
    {
        var command = new RootCommand("Review command test.") { ReviewCommand.Create(environment, terminal.Terminal()) };
        var help = (HelpAction)command.Options.OfType<HelpOption>().First().Action!;
        help.MaxWidth = int.MaxValue;

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Signal handlers are process-wide, so test invocations leave Ctrl+C and SIGTERM to the test host.
        var configuration = new InvocationConfiguration { Output = output, Error = error, ProcessTerminationTimeout = null };
        int exitCode = await command.Parse(["review", .. args]).InvokeAsync(configuration, ct);
        return new CommandLineRun(exitCode, output.ToString(), error.ToString());
    }
}
