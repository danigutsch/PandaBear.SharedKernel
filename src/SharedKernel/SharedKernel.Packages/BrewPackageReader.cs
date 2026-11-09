using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>Reads the installed Homebrew formulae and casks with <c>brew list --versions</c>. It changes nothing.</summary>
/// <remarks>
/// Each line is <c>name version</c>. Homebrew keeps old versions installed until they are cleaned up, and then prints
/// <c>name version1 version2</c>. A plan cannot tell which version is meant, so more than one installed version is an
/// error that asks for <c>brew cleanup</c>, the same as a package listed twice. <c>brew</c> prints nothing when nothing
/// is installed, so an empty output is an empty list; a <c>brew</c> that prints nothing for another reason cannot be told
/// apart, which is why a non-zero exit is always an error.
/// </remarks>
public sealed class BrewPackageReader : IPackageReader
{
    private static readonly string[] Arguments = ["list", "--versions"];
    private readonly IProcessRunner _runner;
    private readonly string _brew;

    /// <summary>Creates a reader.</summary>
    /// <param name="runner">Runs <c>brew</c>.</param>
    /// <param name="brewPath">The absolute path of <c>brew</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runner"/> or <paramref name="brewPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="brewPath"/> is not an absolute path.</exception>
    public BrewPackageReader(IProcessRunner runner, string brewPath)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
        _brew = PackageReaderSupport.RequireAbsolute(brewPath, nameof(brewPath));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<InstalledPackage>>> ListInstalled(CancellationToken cancellationToken)
    {
        var (output, failure) = await PackageReaderSupport.Run(_runner, _brew, "brew", Arguments, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return PackageReaderSupport.Failure(failure);
        }

        List<InstalledPackage> packages = [];
        var lineNumber = 0;
        foreach (var line in output!.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            lineNumber++;
            var parts = line.TrimEnd('\r').Split(' ');
            if (parts.Length < 2)
            {
                return PackageReaderSupport.Failure($"brew line {lineNumber} does not have a name and a version.");
            }

            if (parts.Length > 2)
            {
                return PackageReaderSupport.Failure($"brew line {lineNumber} lists more than one installed version; run brew cleanup, because a plan cannot tell which one is meant.");
            }

            var package = PackageReaderSupport.TryCreate(parts[0], parts[1]);
            if (package is null)
            {
                return PackageReaderSupport.Failure($"brew line {lineNumber} has an invalid formula or version.");
            }

            packages.Add(package);
        }

        // A formula and a cask can share a name; brew then lists both, and a plan cannot tell which is meant.
        var duplicate = packages.GroupBy(static p => p.Name).FirstOrDefault(static g => g.Count() > 1);
        return duplicate is not null
            ? PackageReaderSupport.Failure($"brew listed {duplicate.Key} more than once; a formula and a cask can share a name, so remove one of them, because a plan cannot tell which one is meant.")
            : PackageReaderSupport.Finish("brew", packages);
    }
}
