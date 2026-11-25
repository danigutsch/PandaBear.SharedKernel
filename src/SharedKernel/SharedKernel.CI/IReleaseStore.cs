namespace SharedKernel.CI;

/// <summary>Lists and deletes the releases of one forge repository. Deleting a release keeps its tag.</summary>
public interface IReleaseStore
{
    /// <summary>Lists every release, drafts included, of the repository.</summary>
    /// <param name="ct">Cancels the listing.</param>
    /// <returns>The releases in the order the forge lists them.</returns>
    /// <exception cref="HttpRequestException">The forge refused a request.</exception>
    /// <exception cref="InvalidDataException">The forge returned a list that is not valid or too large.</exception>
    /// <exception cref="TimeoutException">A request took too long.</exception>
    Task<IReadOnlyList<RepositoryRelease>> List(CancellationToken ct);

    /// <summary>Deletes <paramref name="release"/> and its assets, but not its tag.</summary>
    /// <param name="release">A release that <see cref="List"/> returned.</param>
    /// <param name="ct">Cancels the deletion.</param>
    /// <exception cref="HttpRequestException">The forge refused the deletion, or the connection failed; the message names the request.</exception>
    /// <exception cref="TimeoutException">The request took too long; the release may or may not be gone.</exception>
    Task Delete(RepositoryRelease release, CancellationToken ct);
}
