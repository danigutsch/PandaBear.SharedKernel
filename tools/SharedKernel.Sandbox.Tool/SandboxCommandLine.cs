using System.CommandLine;
using PandaBear.Tooling;

namespace PandaBear.Sandbox.Tool;

/// <summary>Builds the <c>sharedkernel-sandbox</c> command tree.</summary>
internal static class SandboxCommandLine
{
    private const int FailureExitCode = 2;

    /// <summary>Creates the root command.</summary>
    /// <param name="workingDirectory">The directory that a relative <c>--root</c> and the default root are resolved from.</param>
    /// <param name="environment">The operations the probes try.</param>
    /// <param name="beforeSettingsCommit">Runs before the settings file is replaced; for tests of the failure path.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static RootCommand Create(string workingDirectory, ISandboxEnvironment environment, Action? beforeSettingsCommit = null)
    {
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentNullException.ThrowIfNull(environment);

        var rootOption = new Option<string>("--root")
        {
            Description = "Repository root directory that holds .config/sharedkernel-sandbox.json. Defaults to the current directory.",
            HelpName = "path",
        };
        var doctor = new Command("doctor", "Probe what this environment allows and explain what it means for .NET tools. Changes nothing that lasts.")
        {
            rootOption,
        };
        doctor.SetAction(async (parseResult, cancellationToken) =>
        {
            var error = parseResult.InvocationConfiguration.Error;
            try
            {
                var root = Path.GetFullPath(parseResult.GetValue(rootOption) ?? ".", workingDirectory);

                // A misspelled root would silently skip the denied-read check, which is the security-relevant one.
                if (!Directory.Exists(root))
                {
                    throw new InvalidOperationException("The root directory does not exist.");
                }

                var configuration = await SandboxConfigurationFile.Read(root, cancellationToken).ConfigureAwait(false);
                return await DoctorCommand.Run(environment, configuration, parseResult.InvocationConfiguration.Output, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                await error.WriteLineAsync(ControlCharacters.Escape(exception.Message).AsMemory(), cancellationToken).ConfigureAwait(false);
                return FailureExitCode;
            }
        });

        var rootCommand = new RootCommand("Probes what the current sandboxed environment allows and explains what that means for .NET tools.");

        // Consumer caller tests parse command lines with [diagram] without running them.
        rootCommand.Directives.Add(new DiagramDirective());
        rootCommand.Subcommands.Add(doctor);
        rootCommand.Subcommands.Add(SettingsCommand.Create(workingDirectory, environment, beforeSettingsCommit));
        return rootCommand;
    }
}
