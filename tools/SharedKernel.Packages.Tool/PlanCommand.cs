using System.Globalization;

namespace PandaBear.Packages.Tool;

/// <summary>
/// Implements <c>plan</c>: reads the declaration, has <see cref="PackageInventory"/> compare it with the installed
/// packages, and prints what is missing or at another version. It changes nothing.
/// </summary>
internal static class PlanCommand
{
    /// <summary>The exit code when a declared package is missing or at another version.</summary>
    public const int DifferencesExitCode = 1;

    /// <summary>The exit code when the result cannot be known: the configuration is invalid or a tool is missing or cannot be read.</summary>
    public const int FailureExitCode = 2;

    /// <summary>
    /// Returns 0 when everything declared is installed, <see cref="DifferencesExitCode"/> when something is missing or
    /// differs, and <see cref="FailureExitCode"/> when a tool cannot be found or read or the configuration is invalid.
    /// Nothing is run before the configuration is valid.
    /// </summary>
    public static async Task<int> Run(string root, PackageInventory inventory, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var configuration = await PackagesConfiguration.Read(root, cancellationToken).ConfigureAwait(false);
        var report = await inventory.Plan(configuration.Packages, cancellationToken).ConfigureAwait(false);
        if (report.MissingTools.Count > 0)
        {
            await PackagesCommandLine.WriteMissing(error, report.MissingTools, cancellationToken).ConfigureAwait(false);
            return FailureExitCode;
        }

        foreach (var manager in report.Managers)
        {
            if (manager.Error is not null)
            {
                await PackagesCommandLine.WriteError(error, manager.Manager, manager.Error, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var id = PackageManagers.Id(manager.Manager);
            foreach (var change in manager.Changes)
            {
                var line = change.Kind == PackageChangeKind.Install
                    ? $"{id}: install {change.Requirement}"
                    : $"{id}: change {change.Requirement.Name} from {change.InstalledVersion} to {change.Requirement.Version}";
                await output.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }

        if (!report.IsComplete)
        {
            return FailureExitCode;
        }

        var changes = report.ChangeCount;
        await output.WriteLineAsync(
            (changes == 0
                ? string.Create(CultureInfo.InvariantCulture, $"All {report.DeclaredCount} declared packages are installed.")
                : string.Create(CultureInfo.InvariantCulture, $"{changes} of {report.DeclaredCount} declared packages need a change.")).AsMemory(),
            cancellationToken).ConfigureAwait(false);
        return changes == 0 ? 0 : DifferencesExitCode;
    }
}
