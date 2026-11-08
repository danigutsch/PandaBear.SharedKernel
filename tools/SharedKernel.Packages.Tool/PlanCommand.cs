using System.Globalization;
using PandaBear.Tooling;

namespace PandaBear.Packages.Tool;

/// <summary>
/// Implements <c>plan</c>: reads the declaration, checks that every package has one owner, reads what each declared
/// manager has installed, and prints what is missing or at another version. It changes nothing.
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
    public static async Task<int> Run(
        string root,
        Func<string, string?> resolveExecutable,
        IProcessRunner runner,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var declared = await PackagesConfigurationFile.Read(root, cancellationToken).ConfigureAwait(false);

        // Resolve every tool first: a missing tool fails the run before any tool is started.
        var readers = new List<(PackageManagerKind Manager, IPackageReader Reader)>();
        var missing = false;
        foreach (var manager in declared.Managers)
        {
            var command = Command(manager);
            var path = resolveExecutable(command);
            if (path is null)
            {
                await error.WriteLineAsync($"{command} was not found on PATH; {PackageManagers.Id(manager)} packages are declared.".AsMemory(), cancellationToken).ConfigureAwait(false);
                missing = true;
                continue;
            }

            readers.Add((manager, CreateReader(manager, runner, path)));
        }

        if (missing)
        {
            return FailureExitCode;
        }

        var failed = false;
        var changes = 0;
        var total = 0;
        foreach (var (manager, reader) in readers)
        {
            var id = PackageManagers.Id(manager);
            var requirements = declared.For(manager);
            total += requirements.Count;
            var installed = await reader.ListInstalled(cancellationToken).ConfigureAwait(false);
            if (installed.IsFailure)
            {
                await error.WriteLineAsync($"{id}: {ControlCharacters.Escape(installed.ErrorDetails?.Detail ?? "could not read the installed packages.")}".AsMemory(), cancellationToken).ConfigureAwait(false);
                failed = true;
                continue;
            }

            foreach (var change in PackagePlanner.Plan(requirements, installed.Value))
            {
                changes++;
                var line = change.Kind == PackageChangeKind.Install
                    ? $"{id}: install {change.Requirement}"
                    : $"{id}: change {change.Requirement.Name} from {change.InstalledVersion} to {change.Requirement.Version}";
                await output.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }

        if (failed)
        {
            return FailureExitCode;
        }

        await output.WriteLineAsync(
            (changes == 0
                ? string.Create(CultureInfo.InvariantCulture, $"All {total} declared packages are installed.")
                : string.Create(CultureInfo.InvariantCulture, $"{changes} of {total} declared packages need a change.")).AsMemory(),
            cancellationToken).ConfigureAwait(false);
        return changes == 0 ? 0 : DifferencesExitCode;
    }

    private static string Command(PackageManagerKind manager) => manager switch
    {
        PackageManagerKind.Apt => "dpkg-query",
        PackageManagerKind.Cargo => "cargo",
        PackageManagerKind.Npm => "npm",
        PackageManagerKind.Flatpak => "flatpak",
        _ => throw new ArgumentOutOfRangeException(nameof(manager)),
    };

    private static IPackageReader CreateReader(PackageManagerKind manager, IProcessRunner runner, string path) => manager switch
    {
        PackageManagerKind.Apt => new AptPackageReader(runner, path),
        PackageManagerKind.Cargo => new CargoPackageReader(runner, path),
        PackageManagerKind.Npm => new NpmPackageReader(runner, path),
        PackageManagerKind.Flatpak => new FlatpakPackageReader(runner, path),
        _ => throw new ArgumentOutOfRangeException(nameof(manager)),
    };
}
