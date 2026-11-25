namespace SharedKernel.CI;

/// <summary>
/// Keeps the newest prereleases of a forge repository and the protected versions, and deletes the other prereleases but
/// not their tags. Stable releases and drafts are never touched.
/// </summary>
public static class PrereleasePruner
{
    /// <summary>
    /// Lists every release, writes the plan to <paramref name="output"/>, and with <see cref="PrereleasePruningRequest.Force"/>
    /// deletes the planned releases one at a time, stopping at the first failure.
    /// </summary>
    /// <param name="request">The repository name, how many to keep, the protected versions, and whether to delete.</param>
    /// <param name="releases">The releases of the repository.</param>
    /// <param name="output">Receives the plan and each deletion.</param>
    /// <param name="error">Receives a warning for each protected version without a release.</param>
    /// <param name="ct">Cancels the requests.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="HttpRequestException">A request failed; for a deletion the message names the releases already deleted.</exception>
    /// <exception cref="InvalidDataException">The release list is not valid.</exception>
    /// <exception cref="TimeoutException">Listing took too long.</exception>
    public static Task Prune(PrereleasePruningRequest request, IReleaseStore releases, TextWriter output, TextWriter error, CancellationToken ct) =>
        PrereleasePruning.Run(request, releases, output, error, ct);
}
