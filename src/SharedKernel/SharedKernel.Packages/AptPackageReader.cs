using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>Reads the installed packages of a Debian-based system with <c>dpkg-query</c>. It changes nothing.</summary>
public sealed class AptPackageReader : IPackageReader
{
    private static readonly string[] Arguments = ["-W", "-f=${binary:Package}\\t${Version}\\t${db:Status-Abbrev}\\n"];
    private readonly IProcessRunner _runner;
    private readonly string _dpkgQuery;

    /// <summary>Creates a reader.</summary>
    /// <param name="runner">Runs <c>dpkg-query</c>.</param>
    /// <param name="dpkgQueryPath">The absolute path of <c>dpkg-query</c>, such as <c>/usr/bin/dpkg-query</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runner"/> or <paramref name="dpkgQueryPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="dpkgQueryPath"/> is not an absolute path.</exception>
    public AptPackageReader(IProcessRunner runner, string dpkgQueryPath)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
        _dpkgQuery = PackageReaderSupport.RequireAbsolute(dpkgQueryPath, nameof(dpkgQueryPath));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<InstalledPackage>>> ListInstalled(CancellationToken cancellationToken)
    {
        var (output, failure) = await PackageReaderSupport.Run(_runner, _dpkgQuery, "dpkg-query", Arguments, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return PackageReaderSupport.Failure(failure);
        }

        List<InstalledPackage> packages = [];
        var lineNumber = 0;
        foreach (var line in output!.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            lineNumber++;
            var parts = line.Split('\t');
            if (parts.Length != 3 || parts[2].Length != 3)
            {
                return PackageReaderSupport.Failure($"dpkg-query line {lineNumber} does not have a name, a version, and a three-letter status.");
            }

            // Status abbreviation: wanted action, current state, error flag. A held package ("hi ") is installed.
            switch (parts[2][1])
            {
                case 'n' or 'c':
                    // Not installed, or only its configuration files remain.
                    continue;
                case 'i' when parts[2][2] == ' ':
                    break;
                default:
                    // Half-installed, unpacked, or flagged for reinstallation: report it instead of hiding it.
                    return PackageReaderSupport.Failure($"dpkg-query line {lineNumber} reports a package that is only partly installed; repair it with dpkg first.");
            }

            var package = PackageReaderSupport.TryCreate(parts[0], parts[1]);
            if (package is null)
            {
                return PackageReaderSupport.Failure($"dpkg-query line {lineNumber} has an invalid package name or version.");
            }

            packages.Add(package);
        }

        return PackageReaderSupport.Finish("dpkg-query", packages);
    }
}
