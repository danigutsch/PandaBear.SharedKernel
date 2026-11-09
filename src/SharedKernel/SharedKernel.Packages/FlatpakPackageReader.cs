using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>Reads the installed Flatpak applications with <c>flatpak list --app</c>. It changes nothing.</summary>
public sealed class FlatpakPackageReader : IPackageReader
{
    private static readonly string[] Arguments = ["list", "--app", "--columns=application,version,branch"];
    private readonly IProcessRunner _runner;
    private readonly string _flatpak;

    /// <summary>Creates a reader.</summary>
    /// <param name="runner">Runs <c>flatpak</c>.</param>
    /// <param name="flatpakPath">The absolute path of <c>flatpak</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runner"/> or <paramref name="flatpakPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flatpakPath"/> is not an absolute path.</exception>
    public FlatpakPackageReader(IProcessRunner runner, string flatpakPath)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
        _flatpak = PackageReaderSupport.RequireAbsolute(flatpakPath, nameof(flatpakPath));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<InstalledPackage>>> ListInstalled(CancellationToken cancellationToken)
    {
        var (output, failure) = await PackageReaderSupport.Run(_runner, _flatpak, "flatpak", Arguments, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return PackageReaderSupport.Failure(failure);
        }

        // An application without a version text is identified by its branch, such as "stable".
        List<InstalledPackage> packages = [];
        var lineNumber = 0;
        foreach (var line in output!.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            lineNumber++;
            var parts = line.Split('\t');
            if (parts.Length != 3)
            {
                return PackageReaderSupport.Failure($"flatpak line {lineNumber} does not have three fields.");
            }

            var package = PackageReaderSupport.TryCreate(parts[0], parts[1].Length > 0 ? parts[1] : parts[2]);
            if (package is null)
            {
                return PackageReaderSupport.Failure($"flatpak line {lineNumber} has an invalid application or version.");
            }

            packages.Add(package);
        }

        return PackageReaderSupport.Finish("flatpak", packages);
    }
}
