using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>Reads the packages that one package manager reports as installed. Implementations change nothing.</summary>
public interface IPackageReader
{
    /// <summary>Lists the installed packages, ordered by name.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The packages, or an error when the command fails or prints something that cannot be understood.</returns>
    Task<Result<IReadOnlyList<InstalledPackage>>> ListInstalled(CancellationToken cancellationToken);
}
