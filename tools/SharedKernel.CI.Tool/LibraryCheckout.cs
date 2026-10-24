using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>Requires the library root to be a clean Git checkout, at its top level, of the pinned revision.</summary>
internal static class LibraryCheckout
{
    private const int MaxListedChanges = 20;

    /// <summary>Checks, in order, that the root is the top level of a work tree, that HEAD is the revision, and that nothing is changed.</summary>
    /// <exception cref="InvalidOperationException">A check failed, or Git failed.</exception>
    public static async Task Verify(Func<string[], string, CancellationToken, Task<string>> git, string libraryRoot, string revision, CancellationToken ct)
    {
        if (!Directory.Exists(libraryRoot))
        {
            throw new InvalidOperationException($"The library root does not exist: {libraryRoot}.");
        }

        // At the top level, git prints "true" and then an empty prefix; in a subdirectory the prefix names it, and in a
        // .git directory or a bare repository it prints "false".
        var location = Lines(await git(["rev-parse", "--is-inside-work-tree", "--show-prefix"], libraryRoot, ct).ConfigureAwait(false));
        if (location is not ["true", "", ..])
        {
            throw new InvalidOperationException($"The library root is not the top level of a Git work tree: {libraryRoot}.");
        }

        var head = (await git(["rev-parse", "--verify", "HEAD"], libraryRoot, ct).ConfigureAwait(false)).Trim();
        if (!string.Equals(head, revision, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The library root's HEAD is {ControlCharacters.Escape(head)}, but the pin names libraryRevision {revision}: {libraryRoot}.");
        }

        var changes = Lines(await git(["status", "--porcelain"], libraryRoot, ct).ConfigureAwait(false))
            .Where(static line => line.Length > 0)
            .ToArray();
        if (changes.Length > 0)
        {
            var listed = string.Join(Environment.NewLine, changes.Take(MaxListedChanges).Select(ControlCharacters.Escape));
            var more = changes.Length > MaxListedChanges ? Environment.NewLine + $"and {changes.Length - MaxListedChanges} more" : string.Empty;
            throw new InvalidOperationException(
                $"The library root has uncommitted changes, so it is not the pinned revision {revision}: {libraryRoot}{Environment.NewLine}{listed}{more}");
        }
    }

    private static string[] Lines(string output) => [.. output.Split('\n').Select(static line => line.TrimEnd('\r'))];
}
