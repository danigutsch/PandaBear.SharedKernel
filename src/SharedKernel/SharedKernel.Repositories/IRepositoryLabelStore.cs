namespace SharedKernel.Repositories;

/// <summary>
/// The labels of one repository on a forge. <see cref="RepositoryLabelSync"/> reads them and creates or updates the
/// configured labels through this interface; a provider package implements it for its forge.
/// </summary>
public interface IRepositoryLabelStore
{
    /// <summary>Gets the repository name that messages show, such as <c>owner/name</c>.</summary>
    string Repository { get; }

    /// <summary>Lists every label of the repository.</summary>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    /// <returns>The labels, in the order the forge returns them.</returns>
    Task<IReadOnlyList<RepositoryLabel>> List(CancellationToken cancellationToken);

    /// <summary>Creates a label.</summary>
    /// <param name="label">The label to create.</param>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    Task Create(RepositoryLabel label, CancellationToken cancellationToken);

    /// <summary>Updates the label named <paramref name="currentName"/> to <paramref name="label"/>.</summary>
    /// <param name="currentName">The name the forge stores, which can differ from the configured name in case.</param>
    /// <param name="label">The configured label.</param>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    Task Update(string currentName, RepositoryLabel label, CancellationToken cancellationToken);
}
