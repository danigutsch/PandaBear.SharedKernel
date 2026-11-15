using System.Globalization;
using System.Net;
using PandaBear.InputNormalization;

namespace PandaBear.CI;

/// <summary>
/// Keeps the newest prereleases of a forge repository and the protected versions, and deletes the other prereleases
/// through an <see cref="IReleaseStore"/>. Tags are never deleted, and stable releases and drafts are never touched.
/// </summary>
internal static class PrereleasePruning
{
    /// <summary>
    /// Reads every release, writes the plan to <paramref name="output"/>, and with <see cref="PrereleasePruningRequest.Force"/>
    /// deletes the planned releases one at a time, stopping at the first failure.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="HttpRequestException">A request failed; for a deletion the message names the releases already deleted.</exception>
    /// <exception cref="InvalidDataException">A release list is not the expected JSON, or too large.</exception>
    /// <exception cref="TimeoutException">A list request took too long.</exception>
    public static async Task Run(PrereleasePruningRequest request, IReleaseStore store, TextWriter output, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var releases = await store.List(ct).ConfigureAwait(false);
        if (releases.Any(static release => release is null))
        {
            throw new InvalidDataException("The release list is not valid: a release is null.");
        }

        var plan = PrereleaseRetentionPlan.Create(releases, request.Keep, request.Protected);

        foreach (var version in request.Protected)
        {
            if (!releases.Any(release => string.Equals(release.TagName, "v" + version, StringComparison.Ordinal)))
            {
                await error.WriteLineAsync($"warning: protected version {version} has no release v{version}.".AsMemory(), CancellationToken.None).ConfigureAwait(false);
            }
        }

        await output.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"{request.Repository}: {Count(plan.Prereleases.Count, "prerelease")}; keeping the newest {plan.Newest.Count} and {plan.Protected.Count} protected.").AsMemory(), CancellationToken.None).ConfigureAwait(false);
        foreach (var release in plan.Prereleases)
        {
            var action = plan.Newest.Contains(release) ? "keep    {0}  {1}  newest" : plan.Protected.Contains(release) ? "keep    {0}  {1}  protected" : "delete  {0}  {1}";
            await output.WriteLineAsync((string.Format(CultureInfo.InvariantCulture, action, Tag(release), Date(release))).AsMemory(), CancellationToken.None).ConfigureAwait(false);
        }

        await output.WriteLineAsync(($"Ignored {Count(plan.Stable, "stable release")} and {Count(plan.Drafts, "draft")}.").AsMemory(), CancellationToken.None).ConfigureAwait(false);
        if (!request.Force)
        {
            await output.WriteLineAsync(($"preview: would delete {Count(plan.Delete.Count, "prerelease")}; --force deletes them. Tags stay.").AsMemory(), CancellationToken.None).ConfigureAwait(false);
            return;
        }

        List<string> deleted = [];
        foreach (var release in plan.Delete)
        {
            await Delete(store, release, deleted, ct).ConfigureAwait(false);
            deleted.Add(Tag(release));
            await output.WriteLineAsync(("deleted " + Tag(release)).AsMemory(), CancellationToken.None).ConfigureAwait(false);
        }

        await output.WriteLineAsync(($"Deleted {Count(deleted.Count, "prerelease")}; their tags stay.").AsMemory(), CancellationToken.None).ConfigureAwait(false);
    }

    private static string Tag(RepositoryRelease release) => ControlCharacters.Escape(release.TagName);

    private static string Date(RepositoryRelease release) => release.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Count(int count, string noun) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");

    // Nothing is retried: any failure stops the run, and the message names what was deleted before it.
    private static async Task Delete(IReleaseStore store, RepositoryRelease release, List<string> deleted, CancellationToken ct)
    {
        HttpStatusCode? status;
        Exception inner;
        try
        {
            await store.Delete(release, ct).ConfigureAwait(false);
            return;
        }
        catch (HttpRequestException exception)
        {
            status = exception.StatusCode;
            inner = exception;
        }
        catch (TimeoutException exception)
        {
            status = null;
            inner = exception;
        }

        // The release may or may not be gone after a timeout or a lost connection, so it is not counted as deleted.
        throw new HttpRequestException(
            $"{inner.Message} The release was {Tag(release)}.{Environment.NewLine}"
            + $"Deleted before the failure: {(deleted.Count == 0 ? "none" : string.Join(", ", deleted))}.{Environment.NewLine}"
            + "No further release was deleted.",
            inner.InnerException,
            status);
    }
}
