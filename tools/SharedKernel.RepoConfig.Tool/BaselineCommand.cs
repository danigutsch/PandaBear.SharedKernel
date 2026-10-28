using System.Globalization;
using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Implements <c>baseline verify</c> and <c>baseline apply</c>.</summary>
internal static class BaselineCommand
{
    /// <summary>Reports each managed file that differs from the baseline and returns 1 when any does.</summary>
    public static async Task<int> Verify(string rootPath, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var (_, entries) = await Plan(rootPath, cancellationToken).ConfigureAwait(false);
        var drifted = entries.Where(static entry => entry.Status != BaselineStatus.Current).ToArray();
        if (drifted.Length == 0)
        {
            await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Repository baseline matches {entries.Count} files.").AsMemory(), cancellationToken).ConfigureAwait(false);
            return 0;
        }

        await error.WriteLineAsync("Repository baseline verification failed:".AsMemory(), cancellationToken).ConfigureAwait(false);
        foreach (var entry in drifted)
        {
            var reason = entry.Status switch
            {
                BaselineStatus.Missing => "missing.",
                BaselineStatus.Outdated => "differs from the baseline.",
                BaselineStatus.Modified => "was edited after the baseline wrote it.",
                _ => "exists without the baseline marker.",
            };
            await error.WriteLineAsync($"- {ControlCharacters.Escape(entry.Path)}: {reason}".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        return 1;
    }

    /// <summary>
    /// Previews the changes, or with <paramref name="force"/> writes them in one rollback batch. A conflict anywhere
    /// writes nothing and returns 1.
    /// </summary>
    public static async Task<int> Apply(string rootPath, bool force, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var (baseline, entries) = await Plan(rootPath, cancellationToken).ConfigureAwait(false);
        var changes = entries.Where(static entry => entry.Status != BaselineStatus.Current && !entry.IsConflict).ToArray();
        var conflicts = entries.Where(static entry => entry.IsConflict).ToArray();
        int created = changes.Count(static e => e.Status == BaselineStatus.Missing),
            updated = changes.Count(static e => e.Status == BaselineStatus.Outdated),
            overwritten = changes.Length - created - updated,
            unchanged = entries.Count - changes.Length - conflicts.Length;

        foreach (var conflict in conflicts)
        {
            var reason = conflict.Status == BaselineStatus.Modified ? "was edited after the baseline wrote it" : "exists without the baseline marker";
            await error.WriteLineAsync($"conflict: {ControlCharacters.Escape(conflict.Path)} {reason}; set overwriteExisting on its entry to replace it.".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        if (conflicts.Length > 0)
        {
            // Nothing is written, so the previewed changes are not applied either.
            return 1;
        }

        if (force && changes.Length > 0)
        {
            Write(rootPath, changes);
        }

        foreach (var change in changes)
        {
            var (verb, previewVerb) = change.Status switch
            {
                BaselineStatus.Missing => ("Created", "create"),
                BaselineStatus.Outdated => ("Updated", "update"),
                _ => ("Overwrote", "overwrite"),
            };
            var path = ControlCharacters.Escape(change.Path);
            await output.WriteLineAsync((force ? $"{verb} {path}." : $"preview: would {previewVerb} {path}.").AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        await output.WriteLineAsync(
            (force
                ? string.Create(CultureInfo.InvariantCulture, $"{baseline.RepositoryName}: {created} created, {updated} updated, {overwritten} overwritten, and {unchanged} unchanged files.")
                : string.Create(CultureInfo.InvariantCulture, $"preview: {created} would be created, {updated} updated, {overwritten} overwritten, and {unchanged} unchanged{(changes.Length > 0 ? "; --force writes them" : string.Empty)}.")).AsMemory(),
            cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private static async Task<(BaselineConfiguration Baseline, IReadOnlyList<BaselineEntry> Entries)> Plan(string rootPath, CancellationToken cancellationToken)
    {
        var configuration = await ConsumerConfigurationFile.Read(rootPath, cancellationToken).ConfigureAwait(false);
        var baseline = configuration.Baseline
            ?? throw new InvalidOperationException($"{ConsumerConfigurationFile.RelativePath} has no baseline section.");
        return (baseline, await BaselinePlanner.Plan(rootPath, baseline, cancellationToken).ConfigureAwait(false));
    }

    private static void Write(string rootPath, BaselineEntry[] changes)
    {
        var root = Path.GetFullPath(rootPath);
        foreach (var change in changes)
        {
            RollbackFileWriteBatch.ValidateWritePath(root, change.FullPath);
            Directory.CreateDirectory(Path.GetDirectoryName(change.FullPath)!);
        }

        RollbackFileWriteBatch.Apply(root, [.. changes.Select(static c => new AtomicFileWrite(c.FullPath, c.Desired, c.Existing))]);
    }
}
