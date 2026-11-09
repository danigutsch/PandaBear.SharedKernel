using System.CommandLine;
using System.Globalization;
using PandaBear.Tooling;

namespace PandaBear.Sandbox.Tool;

/// <summary>The <c>settings plan|apply|verify</c> command group.</summary>
internal static class SettingsCommand
{
    private const int DriftExitCode = 1;
    private const int FailureExitCode = 2;

    /// <summary>Creates the <c>settings</c> command.</summary>
    /// <param name="workingDirectory">The directory that a relative <c>--root</c> and the default root are resolved from.</param>
    /// <param name="environment">Tells the home folders that <c>allowWrite</c> must not cover.</param>
    /// <param name="beforeCommit">Runs before the settings file is replaced; for tests of the failure path.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static Command Create(string workingDirectory, ISandboxEnvironment environment, Action? beforeCommit = null)
    {
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentNullException.ThrowIfNull(environment);
        var settings = new Command("settings", "Generate, verify, and merge the sandbox section of an agent settings file from .config/sharedkernel-sandbox.json.")
        {
            Build("plan", "Show what would be added to the settings file. Writes nothing.", SettingsMode.Plan, workingDirectory, environment, beforeCommit),
            Build("apply", "Add the managed entries to the settings file: a preview unless --force is given. Never removes an entry that is not listed for removal.", SettingsMode.Apply, workingDirectory, environment, beforeCommit),
            Build("verify", "Report managed entries that the settings file lacks. Exits 1 on drift.", SettingsMode.Verify, workingDirectory, environment, beforeCommit),
        };
        return settings;
    }

    private static Command Build(string name, string description, SettingsMode mode, string workingDirectory, ISandboxEnvironment environment, Action? beforeCommit)
    {
        var rootOption = new Option<string>("--root")
        {
            Description = "Repository root directory that holds .config/sharedkernel-sandbox.json. Defaults to the current directory.",
            HelpName = "path",
        };
        var sockets = new Option<bool>("--accept-risk-allow-all-unix-sockets")
        {
            Description = "Accept allowAllUnixSockets: true, which opens the container engine, the ssh and gpg agents, and the user bus to every sandboxed command.",
        };
        var excluded = new Option<bool>("--accept-risk-excluded-commands")
        {
            Description = "Accept excludedCommands entries: those commands run outside the sandbox with full access.",
        };
        var broadWrite = new Option<bool>("--accept-risk-broad-write")
        {
            Description = "Accept an allowWrite entry for the home folder, a folder above it, or a top-level system folder.",
        };
        var sensitiveWrite = new Option<bool>("--accept-risk-sensitive-write")
        {
            Description = "Accept an allowWrite entry for shell startup files, ssh and gpg folders, autostart, or git hooks and configuration.",
        };
        var denyRead = new Option<bool>("--accept-risk-remove-deny-read")
        {
            Description = "Accept removing denyRead entries, which makes the paths readable to sandboxed commands.",
        };
        var force = new Option<bool>("--force")
        {
            Description = "Write the settings file. Without it, apply only previews.",
        };
        var command = new Command(name, description) { rootOption, sockets, excluded, denyRead, broadWrite, sensitiveWrite };
        if (mode is SettingsMode.Apply)
        {
            command.Options.Add(force);
        }

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var error = parseResult.InvocationConfiguration.Error;
            try
            {
                var root = Path.GetFullPath(parseResult.GetValue(rootOption) ?? ".", workingDirectory);
                if (!Directory.Exists(root))
                {
                    throw new InvalidOperationException("The root directory does not exist.");
                }

                var flags = new SettingsRiskFlags(parseResult.GetValue(sockets), parseResult.GetValue(excluded), parseResult.GetValue(denyRead), parseResult.GetValue(broadWrite), parseResult.GetValue(sensitiveWrite));
                return await Run(root, mode, parseResult.GetValue(force), flags, environment, parseResult.InvocationConfiguration.Output, beforeCommit, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                await error.WriteLineAsync(ControlCharacters.Escape(exception.Message).AsMemory(), cancellationToken).ConfigureAwait(false);
                return FailureExitCode;
            }
        });
        return command;
    }

    private static async Task<int> Run(string root, SettingsMode mode, bool force, SettingsRiskFlags flags, ISandboxEnvironment environment, TextWriter output, Action? beforeCommit, CancellationToken cancellationToken)
    {
        var configuration = await SandboxConfigurationFile.Read(root, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"{SandboxConfigurationFile.RelativePath} does not exist; the settings command needs its $.settings section.");
        var desired = DesiredSettings.Build(configuration, flags, [environment.HomeDirectory, environment.AccountHomeDirectory ?? string.Empty], enforce: mode is SettingsMode.Apply && force);
        var path = SettingsFileStore.Resolve(root, desired.Target);
        var existing = await SettingsFileStore.Read(path, desired.Target, cancellationToken).ConfigureAwait(false);
        var plan = SettingsMerger.Plan(desired, existing, desired.Target);

        await WriteLine(output, "settings: " + desired.Target, cancellationToken).ConfigureAwait(false);
        foreach (var change in plan.Changes)
        {
            var verb = change.Kind switch
            {
                SettingsChangeKind.Add => "add",
                SettingsChangeKind.Remove => "remove",
                _ => "set",
            };
            await WriteLine(output, $"{verb} {change.JsonPath}: {change.Value}", cancellationToken).ConfigureAwait(false);
        }

        foreach (var warning in desired.RiskWarnings.Concat(plan.Warnings))
        {
            await WriteLine(output, "warning: " + warning, cancellationToken).ConfigureAwait(false);
        }

        if (plan.Changes.Count == 0)
        {
            await WriteLine(output, mode is SettingsMode.Verify ? "No drift: every managed entry is present." : "Up to date: nothing to change.", cancellationToken).ConfigureAwait(false);
            return 0;
        }

        var count = plan.Changes.Count.ToString(CultureInfo.InvariantCulture);
        switch (mode)
        {
            case SettingsMode.Verify:
                await WriteLine(output, $"Drift: {count} managed entries differ. Run 'settings apply --force' to add them.", cancellationToken).ConfigureAwait(false);
                return DriftExitCode;
            case SettingsMode.Apply when force:
                SettingsFileStore.Write(root, desired.Target, plan.Content, plan.ExpectedContent, beforeCommit);
                await WriteLine(output, $"Wrote {count} changes to {desired.Target}.", cancellationToken).ConfigureAwait(false);
                return 0;
            default:
                await WriteLine(output, $"{count} changes planned. Preview only; nothing was written." + (mode is SettingsMode.Apply ? " Pass --force to write." : " Use 'settings apply --force' to write."), cancellationToken).ConfigureAwait(false);
                return 0;
        }
    }

    private static Task WriteLine(TextWriter output, string text, CancellationToken cancellationToken) =>
        output.WriteLineAsync(ControlCharacters.Escape(text).AsMemory(), cancellationToken);
}
