using System.Globalization;
using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Implements <c>baseline verify</c> and <c>baseline apply</c>.</summary>
internal static class BaselineCommand
{
    /// <summary>Reports each managed file and Git setting that differs from the baseline and returns 1 when any does.</summary>
    public static async Task<int> Verify(string rootPath, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var plan = await Plan(rootPath, cancellationToken).ConfigureAwait(false);
        var drifted = plan.Files.Where(static entry => entry.Status != BaselineStatus.Current).ToArray();
        var driftedSettings = plan.Settings.Where(static entry => entry.Status != BaselineStatus.Current).ToArray();
        if (drifted.Length + driftedSettings.Length == 0)
        {
            await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Repository baseline matches {plan.Files.Count + plan.Settings.Count} {plan.Noun}.").AsMemory(), cancellationToken).ConfigureAwait(false);
            return 0;
        }

        await error.WriteLineAsync("Repository baseline verification failed:".AsMemory(), cancellationToken).ConfigureAwait(false);
        foreach (var entry in drifted)
        {
            var reason = entry.Status switch
            {
                BaselineStatus.Missing => "missing.",
                BaselineStatus.Outdated when entry.Executable && Normalize(entry.Existing) == entry.Desired => "is not executable.",
                BaselineStatus.Outdated => "differs from the baseline.",
                BaselineStatus.Modified => "was edited after the baseline wrote it.",
                _ => "exists without the baseline marker.",
            };
            await error.WriteLineAsync($"- {ControlCharacters.Escape(entry.Path)}: {reason}".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        foreach (var entry in driftedSettings)
        {
            var reason = entry.Status == BaselineStatus.Missing ? "not set." : "set to a different value.";
            await error.WriteLineAsync($"- git config {entry.Key}: {reason}".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        return 1;
    }

    /// <summary>
    /// Previews the changes, or with <paramref name="force"/> writes the files in one rollback batch and then sets the
    /// Git settings. A conflict anywhere writes nothing, in a preview too, and returns 1.
    /// </summary>
    public static async Task<int> Apply(string rootPath, bool force, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var plan = await Plan(rootPath, cancellationToken).ConfigureAwait(false);
        var changes = plan.Files.Where(static entry => entry.Status != BaselineStatus.Current && !entry.IsConflict).ToArray();
        var settingChanges = plan.Settings.Where(static entry => entry.Status != BaselineStatus.Current && !entry.IsConflict).ToArray();
        var conflicts = plan.Files.Where(static entry => entry.IsConflict).ToArray();
        var settingConflicts = plan.Settings.Where(static entry => entry.IsConflict).ToArray();
        int created = changes.Count(static e => e.Status == BaselineStatus.Missing) + settingChanges.Count(static e => e.Status == BaselineStatus.Missing),
            updated = changes.Count(static e => e.Status == BaselineStatus.Outdated),
            overwritten = changes.Length + settingChanges.Length - created - updated,
            unchanged = plan.Files.Count + plan.Settings.Count - changes.Length - settingChanges.Length - conflicts.Length - settingConflicts.Length;

        foreach (var conflict in conflicts)
        {
            var reason = conflict.Status == BaselineStatus.Modified ? "was edited after the baseline wrote it" : "exists without the baseline marker";
            await error.WriteLineAsync($"conflict: {ControlCharacters.Escape(conflict.Path)} {reason}; set overwriteExisting on its entry to replace it.".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        foreach (var conflict in settingConflicts)
        {
            await error.WriteLineAsync($"conflict: git config {conflict.Key} is set to a different value; set overwriteExisting on its entry to replace it.".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        if (conflicts.Length + settingConflicts.Length > 0)
        {
            // Nothing is written, so the previewed changes are not applied either.
            return 1;
        }

        if (force)
        {
            if (changes.Length > 0)
            {
                Write(rootPath, changes);
            }

            foreach (var change in settingChanges)
            {
                await BaselineGit.Set(Path.GetFullPath(rootPath), change, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (var change in changes)
        {
            await Report(output, force, change.Status, ControlCharacters.Escape(change.Path), cancellationToken).ConfigureAwait(false);
        }

        foreach (var change in settingChanges)
        {
            await output.WriteLineAsync((force ? $"Set git config {change.Key}." : $"preview: would set git config {change.Key}.").AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        var any = changes.Length + settingChanges.Length > 0;
        await output.WriteLineAsync(
            (force
                ? string.Create(CultureInfo.InvariantCulture, $"{plan.Baseline.RepositoryName}: {created} created, {updated} updated, {overwritten} overwritten, and {unchanged} unchanged {plan.Noun}.")
                : string.Create(CultureInfo.InvariantCulture, $"preview: {created} would be created, {updated} updated, {overwritten} overwritten, and {unchanged} unchanged{(any ? "; --force writes them" : string.Empty)}.")).AsMemory(),
            cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private static Task Report(TextWriter output, bool force, BaselineStatus status, string path, CancellationToken cancellationToken)
    {
        var (verb, previewVerb) = status switch
        {
            BaselineStatus.Missing => ("Created", "create"),
            BaselineStatus.Outdated => ("Updated", "update"),
            _ => ("Overwrote", "overwrite"),
        };
        return output.WriteLineAsync((force ? $"{verb} {path}." : $"preview: would {previewVerb} {path}.").AsMemory(), cancellationToken);
    }

    private static string? Normalize(string? text) => text?.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static async Task<BaselinePlan> Plan(string rootPath, CancellationToken cancellationToken)
    {
        var configuration = await ConsumerConfigurationFile.Read(rootPath, cancellationToken).ConfigureAwait(false);
        var baseline = configuration.Baseline
            ?? throw new InvalidOperationException($"{ConsumerConfigurationFile.RelativePath} has no baseline section.");
        var files = await BaselinePlanner.Plan(rootPath, baseline, cancellationToken).ConfigureAwait(false);
        var settings = baseline.GitSettings.Count == 0
            ? []
            : await BaselineGit.Plan(Path.GetFullPath(rootPath), baseline.GitSettings, cancellationToken).ConfigureAwait(false);
        return new BaselinePlan(baseline, files, settings);
    }

    private static void Write(string rootPath, BaselineEntry[] changes)
    {
        var root = Path.GetFullPath(rootPath);
        List<string> createdDirectories = [];
        try
        {
            foreach (var change in changes)
            {
                RollbackFileWriteBatch.ValidateWritePath(root, change.FullPath);
                Stack<string> missing = [];
                for (var directory = Path.GetDirectoryName(change.FullPath)!; !Directory.Exists(directory); directory = Path.GetDirectoryName(directory)!)
                {
                    missing.Push(directory);
                }

                foreach (var directory in missing)
                {
                    Directory.CreateDirectory(directory);
                    createdDirectories.Add(directory);
                }
            }

            RollbackFileWriteBatch.Apply(root, [.. changes.Select(static c => new AtomicFileWrite(c.FullPath, c.Desired, c.Existing))]);
            if (!OperatingSystem.IsWindows())
            {
                foreach (var change in changes.Where(static c => c.Executable))
                {
                    File.SetUnixFileMode(change.FullPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
            }
        }
        catch
        {
            // Directories this apply created are removed again, deepest first and only when empty.
            foreach (var directory in createdDirectories.AsEnumerable().Reverse())
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    continue;
                }
            }

            throw;
        }
    }

    private sealed record BaselinePlan(BaselineConfiguration Baseline, IReadOnlyList<BaselineEntry> Files, IReadOnlyList<BaselineGitEntry> Settings)
    {
        public string Noun => Settings.Count > 0 ? "files and settings" : "files";
    }
}
