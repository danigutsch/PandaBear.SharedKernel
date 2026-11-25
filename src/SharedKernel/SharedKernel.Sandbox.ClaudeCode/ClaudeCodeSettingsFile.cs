namespace SharedKernel.Sandbox.ClaudeCode;

/// <summary>
/// A Claude Code settings file under a repository root, such as <c>.claude/settings.json</c>: plans the sandbox entries it
/// lacks and writes them. The file is read as strict UTF-8 JSON of at most 1 MiB, is never reached through a symbolic link,
/// and is replaced atomically.
/// </summary>
public sealed class ClaudeCodeSettingsFile
{
    /// <summary>The shared project settings file of Claude Code, relative to the repository root.</summary>
    public const string DefaultTarget = ".claude/settings.json";

    private readonly string _root;
    private readonly string _path;

    private ClaudeCodeSettingsFile(string root, string target, string path)
    {
        _root = root;
        Target = target;
        _path = path;
    }

    /// <summary>Gets the settings file relative to the root, with <c>/</c> separators.</summary>
    public string Target { get; }

    /// <summary>Finds the settings file <paramref name="target"/> under <paramref name="root"/>.</summary>
    /// <param name="root">The repository root; the file must stay inside it.</param>
    /// <param name="target">The settings file relative to <paramref name="root"/>, or <see langword="null"/> for <see cref="DefaultTarget"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The path is not repository-relative, is not a <c>.json</c> file, or is or traverses a symbolic link.</exception>
    public static ClaudeCodeSettingsFile Locate(string root, string? target)
    {
        ArgumentNullException.ThrowIfNull(root);
        target ??= DefaultTarget;
        return new ClaudeCodeSettingsFile(root, target, SettingsFileStore.Resolve(root, target));
    }

    /// <summary>Reads the file and plans the edits that bring <paramref name="desired"/> into it. Writes nothing.</summary>
    /// <param name="desired">The entries the file should contain.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The edits, the warnings, the new content, and the content the file must still have when the plan is written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="desired"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The file is larger than 1 MiB, is not valid UTF-8 or JSON, or a managed key has the wrong type. The message names the
    /// file and the JSON path.
    /// </exception>
    public async Task<SettingsPlan> Plan(DesiredSettings desired, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(desired);
        var existing = await SettingsFileStore.Read(_path, Target, cancellationToken).ConfigureAwait(false);
        return SettingsMerger.Plan(desired, ClaudeCodeSettingsDocument.Parse(existing, Target));
    }

    /// <summary>
    /// Replaces the file with <see cref="SettingsPlan.Content"/>: writes a temporary sibling, flushes it, and renames it over
    /// the file, after checking that the file still has <see cref="SettingsPlan.ExpectedContent"/>. A failure leaves the
    /// original untouched and removes the temporary file and the directories this call created.
    /// </summary>
    /// <param name="plan">A plan from <see cref="Plan"/>.</param>
    /// <param name="beforeCommit">Runs after the temporary file is complete and before the rename; an exception it throws fails the write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The file changed since it was read, the content is larger than 1 MiB, or the path is a symbolic link.</exception>
    public void Write(SettingsPlan plan, Action? beforeCommit = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        SettingsFileStore.Write(_root, Target, plan.Content, plan.ExpectedContent, beforeCommit);
    }
}
