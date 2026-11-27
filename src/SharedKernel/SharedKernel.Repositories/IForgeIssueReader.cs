namespace SharedKernel.Repositories;

/// <summary>Reads issue state and relationships from a forge without changing the repository.</summary>
public interface IForgeIssueReader
{
    /// <summary>Gets the optional metadata available from this provider.</summary>
    ForgeIssueCapabilities Capabilities { get; }

    /// <summary>Lists the repository's open issues, with their available metadata.</summary>
    /// <param name="repository">The repository in <c>owner/name</c> form.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The open issues.</returns>
    /// <exception cref="InvalidOperationException">The provider could not read the repository or returned incomplete data.</exception>
    /// <exception cref="TimeoutException">The read exceeded the provider's time limit.</exception>
    Task<IReadOnlyList<ForgeIssue>> ReadOpenIssues(string repository, CancellationToken cancellationToken);

    /// <summary>Reads an open or closed issue, including issues in other accessible repositories.</summary>
    /// <param name="reference">The issue to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The resolved issue.</returns>
    /// <exception cref="InvalidOperationException">The issue is missing, inaccessible, or incomplete.</exception>
    /// <exception cref="TimeoutException">The read exceeded the provider's time limit.</exception>
    Task<ForgeIssue> ReadIssue(ForgeIssueReference reference, CancellationToken cancellationToken);
}
