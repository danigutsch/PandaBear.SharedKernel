using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>Reads the crates that <c>cargo install</c> installed, with <c>cargo install --list</c>. It changes nothing.</summary>
public sealed class CargoPackageReader : IPackageReader
{
    private static readonly string[] Arguments = ["install", "--list"];
    private readonly IProcessRunner _runner;
    private readonly string _cargo;

    /// <summary>Creates a reader.</summary>
    /// <param name="runner">Runs <c>cargo</c>.</param>
    /// <param name="cargoPath">The absolute path of <c>cargo</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runner"/> or <paramref name="cargoPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="cargoPath"/> is not an absolute path.</exception>
    public CargoPackageReader(IProcessRunner runner, string cargoPath)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
        _cargo = PackageReaderSupport.RequireAbsolute(cargoPath, nameof(cargoPath));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<InstalledPackage>>> ListInstalled(CancellationToken cancellationToken)
    {
        var (output, failure) = await PackageReaderSupport.Run(_runner, _cargo, "cargo", Arguments, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return PackageReaderSupport.Failure(failure);
        }

        // A crate line is "name vX.Y.Z:" or "name vX.Y.Z (source):"; the indented lines below it list its binaries.
        List<InstalledPackage> packages = [];
        var lineNumber = 0;
        foreach (var line in output!.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            lineNumber++;
            if (line[0] is ' ' or '\t')
            {
                continue;
            }

            var parts = line.TrimEnd(':').Split(' ', 3);
            var version = parts.Length >= 2 && parts[1].StartsWith('v') ? parts[1][1..] : string.Empty;
            var package = PackageReaderSupport.TryCreate(parts[0], version);
            if (package is null)
            {
                return PackageReaderSupport.Failure($"cargo line {lineNumber} is not a crate and version.");
            }

            packages.Add(package);
        }

        return PackageReaderSupport.Finish("cargo", packages);
    }
}
