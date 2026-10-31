using System.CommandLine;
using System.CommandLine.Invocation;
using PandaBear.VaultManager.Tool.Cli;
using PandaBear.VaultManager.Tool.IO;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool;

/// <summary>Builds and parses the <c>sharedkernel-vault</c> command tree.</summary>
internal static class VaultCommandLine
{
    /// <summary>
    /// Creates the root command with the <c>inspect</c>, <c>scan</c>, <c>merge</c>, and <c>review</c> commands. Commands
    /// write to the invocation's output and error writers and ask for passwords through <paramref name="environment"/>.
    /// </summary>
    /// <param name="environment">Password prompts, terminal state, and clock of the commands.</param>
    /// <returns>The root command.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="environment"/> is <see langword="null"/>.</exception>
    public static RootCommand Create(VaultCommandEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return new RootCommand("Inspect, scan, merge, review, and organize password vault exports offline.")
        {
            InspectCommand.Create(environment),
            ScanCommand.Create(environment),
            MergeCommand.Create(environment),
            ReviewCommand.Create(environment),
            CatalogCommand.Create(environment),
            ApplyCommand.Create(environment),
        };
    }

    /// <summary>
    /// Parses a command line with the parser settings of the tool: System.CommandLine's defaults without response
    /// files, so an argument that starts with <c>@</c> is a file name like any other.
    /// </summary>
    /// <param name="rootCommand">Root command from <see cref="Create"/>.</param>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The parse result, ready to invoke.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rootCommand"/> or <paramref name="args"/> is <see langword="null"/>.</exception>
    public static ParseResult Parse(RootCommand rootCommand, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(rootCommand);
        ArgumentNullException.ThrowIfNull(args);

        var configuration = new ParserConfiguration
        {
            // Response files are off. System.CommandLine would read "@bitwarden.json" as a response file and split the
            // export into arguments, and the errors about those arguments would print vault values. Every argument of
            // this tool is a file name or an option, and passwords are never arguments, so nothing needs one.
            ResponseFileTokenReplacer = null,
        };
        return rootCommand.Parse(args, configuration);
    }

    /// <summary>
    /// Parses a command line with <see cref="Parse"/> and invokes it. A command line with parse errors gets
    /// System.CommandLine's error output, typo corrections, and help, written through <see cref="PrintableTextWriter"/>.
    /// </summary>
    /// <param name="rootCommand">Root command from <see cref="Create"/>.</param>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="configuration">Output and error writers, and signal handling, of the invocation.</param>
    /// <param name="ct">Cancels the command.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is <see langword="null"/>.</exception>
    public static Task<int> Invoke(RootCommand rootCommand, IReadOnlyList<string> args, InvocationConfiguration configuration, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        ParseResult parseResult = Parse(rootCommand, args);
        if (parseResult.Action is ParseErrorAction)
        {
            // System.CommandLine's parse errors and typo corrections repeat the arguments verbatim, so an argument with an
            // escape sequence or a bidirectional override would reach the terminal. Everything else the tool prints goes
            // through PrintableText, so the error output does too; its text, exit code 1, and streams stay the default.
            configuration = new InvocationConfiguration
            {
                Output = new PrintableTextWriter(configuration.Output),
                Error = new PrintableTextWriter(configuration.Error),
                EnableDefaultExceptionHandler = configuration.EnableDefaultExceptionHandler,
                ProcessTerminationTimeout = configuration.ProcessTerminationTimeout,
            };
        }

        return parseResult.InvokeAsync(configuration, ct);
    }
}
