namespace SharedKernel.Repositories;

/// <summary>Reads and renders the caller's policy files before the scaffold writes its batch.</summary>
internal static class NewRepositoryPolicyFiles
{
    /// <summary>Returns the write-once policy content from the explicitly supplied sources.</summary>
    /// <exception cref="InvalidOperationException">A source path is blank or its content cannot be rendered.</exception>
    internal static async Task<IReadOnlyList<(string Path, string Content)>> Render(
        RepositoryPolicySources? sources,
        BaselineConfiguration baseline,
        CancellationToken cancellationToken)
    {
        if (sources is null)
        {
            return [];
        }

        (string Path, string? Source)[] inputs =
        [
            ("LICENSE", sources.LicenseSource),
            ("SECURITY.md", sources.SecuritySource),
            ("CODEOWNERS", sources.CodeOwnersSource)
        ];
        List<(string Path, string Content)> files = [];
        foreach (var (path, source) in inputs)
        {
            if (source is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                throw new InvalidOperationException($"{path}: a source file path is required.");
            }

            var template = await BaselinePlanner.ReadSourceText(Path.GetFullPath(source), path, source, cancellationToken)
                .ConfigureAwait(false);
            files.Add((path, BaselinePlanner.Substitute(path, template, baseline, sources)));
        }

        return files;
    }
}
