using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>Reads the installed snaps with <c>snap list</c>. It changes nothing.</summary>
/// <remarks>
/// The output is a table: <c>Name Version Rev Tracking Publisher Notes</c>. The header line is checked and then skipped
/// on purpose, so a snap can never be read from it. Base and core snaps such as <c>core24</c> and <c>snapd</c> are
/// installed snaps and are listed. A line that does not have exactly six columns is an error.
/// </remarks>
public sealed class SnapPackageReader : IPackageReader
{
    private const string Header = "Name Version Rev Tracking Publisher Notes";
    private const int ColumnCount = 6;
    private static readonly string[] Arguments = ["list"];
    private readonly IProcessRunner _runner;
    private readonly string _snap;

    /// <summary>Creates a reader.</summary>
    /// <param name="runner">Runs <c>snap</c>.</param>
    /// <param name="snapPath">The absolute path of <c>snap</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runner"/> or <paramref name="snapPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="snapPath"/> is not an absolute path.</exception>
    public SnapPackageReader(IProcessRunner runner, string snapPath)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
        _snap = PackageReaderSupport.RequireAbsolute(snapPath, nameof(snapPath));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<InstalledPackage>>> ListInstalled(CancellationToken cancellationToken)
    {
        var (output, failure) = await PackageReaderSupport.Run(_runner, _snap, "snap", Arguments, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return PackageReaderSupport.Failure(failure);
        }

        // With no snap installed, snap prints its notice on the standard error and nothing on the standard output.
        var lines = output!.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return PackageReaderSupport.Finish("snap", []);
        }

        if (string.Join(' ', Columns(lines[0])) != Header)
        {
            return PackageReaderSupport.Failure("snap line 1 is not the expected header; the table format is not supported.");
        }

        List<InstalledPackage> packages = [];
        for (var index = 1; index < lines.Length; index++)
        {
            var columns = Columns(lines[index]);
            if (columns.Length != ColumnCount)
            {
                return PackageReaderSupport.Failure($"snap line {index + 1} does not have six columns.");
            }

            var package = PackageReaderSupport.TryCreate(columns[0], columns[1]);
            if (package is null)
            {
                return PackageReaderSupport.Failure($"snap line {index + 1} has an invalid snap name or version.");
            }

            packages.Add(package);
        }

        return PackageReaderSupport.Finish("snap", packages);
    }

    private static string[] Columns(string line) => line.Split([' ', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries);
}
