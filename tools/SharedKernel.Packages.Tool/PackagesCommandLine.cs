using System.CommandLine;
using PandaBear.Tooling;

namespace PandaBear.Packages.Tool;

/// <summary>Builds the <c>sharedkernel-packages</c> command tree.</summary>
internal static class PackagesCommandLine
{
    /// <summary>Creates the root command.</summary>
    /// <param name="workingDirectory">The directory that a relative <c>--root</c> and the default root are resolved from.</param>
    /// <param name="resolveExecutable">Finds a program by name and returns its absolute path, or <see langword="null"/>.</param>
    /// <param name="runner">Runs the package tools.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static RootCommand Create(string workingDirectory, Func<string, string?> resolveExecutable, IProcessRunner runner)
    {
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentNullException.ThrowIfNull(resolveExecutable);
        ArgumentNullException.ThrowIfNull(runner);

        var rootOption = new Option<string>("--root")
        {
            Description = "Repository root directory. Defaults to the current directory.",
            HelpName = "path",
        };
        var plan = new Command("plan", "Report the declared packages that are not installed or are installed at another version. Changes nothing.")
        {
            rootOption,
        };
        plan.SetAction((parseResult, cancellationToken) => Execute(
            parseResult,
            (output, error) => PlanCommand.Run(
                Path.GetFullPath(parseResult.GetValue(rootOption) ?? ".", workingDirectory),
                resolveExecutable,
                runner,
                output,
                error,
                cancellationToken),
            cancellationToken));

        var root = new RootCommand("Compares the packages that .config/sharedkernel-packages.json declares per package manager with the installed packages, and refuses a package with more than one owner.");

        // Consumer caller tests parse command lines with [diagram] without running them.
        root.Directives.Add(new DiagramDirective());
        root.Subcommands.Add(plan);
        return root;
    }

    private static async Task<int> Execute(ParseResult parseResult, Func<TextWriter, TextWriter, Task<int>> action, CancellationToken cancellationToken)
    {
        var error = parseResult.InvocationConfiguration.Error;
        try
        {
            return await action(parseResult.InvocationConfiguration.Output, error).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            foreach (var line in exception.Message.TrimEnd('\r', '\n').Split('\n'))
            {
                await error.WriteLineAsync(ControlCharacters.Escape(line.TrimEnd('\r')).AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            return PlanCommand.FailureExitCode;
        }
    }
}
