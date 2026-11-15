using System.Globalization;

namespace PandaBear.Repositories;

/// <summary>
/// Creates the configured repository labels that are missing and updates those that differ. Labels that are not
/// configured are kept.
/// </summary>
public static class RepositoryLabelSync
{
    /// <summary>
    /// Reads the <c>labels</c> section of the consumer configuration under <paramref name="rootPath"/> and the labels
    /// of <paramref name="store"/>, then writes a preview line for each change, or with <paramref name="force"/>
    /// creates and updates the labels, and ends with a summary line.
    /// </summary>
    /// <param name="rootPath">The repository root that holds the consumer configuration.</param>
    /// <param name="store">The labels of the repository on its forge.</param>
    /// <param name="force">Whether to write the changes instead of previewing them.</param>
    /// <param name="output">The writer that receives the progress and summary lines.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The consumer configuration is invalid or has no labels section, or the store fails.</exception>
    public static async Task Run(
        string rootPath,
        IRepositoryLabelStore store,
        bool force,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(output);

        var configuration = await ConsumerConfigurationFile.Read(rootPath, cancellationToken).ConfigureAwait(false);
        var labels = configuration.Labels
            ?? throw new InvalidOperationException($"{ConsumerConfigurationFile.RelativePath} has no labels section.");
        Dictionary<string, RepositoryLabel> remote = new(StringComparer.OrdinalIgnoreCase);
        foreach (var label in await store.List(cancellationToken).ConfigureAwait(false))
        {
            remote.TryAdd(label.Name, label);
        }

        int created = 0, updated = 0;
        foreach (var label in labels)
        {
            if (remote.TryGetValue(label.Name, out var current) && IsCurrent(label, current))
            {
                continue;
            }

            var create = current is null;
            if (create)
            {
                created++;
            }
            else
            {
                updated++;
            }

            if (!force)
            {
                await output.WriteLineAsync($"preview: would {(create ? "create" : "update")} label {label.Name}.".AsMemory(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            await (create
                ? store.Create(label, cancellationToken)
                : store.Update(current!.Name, label, cancellationToken))
                .ConfigureAwait(false);
            await output.WriteLineAsync($"{(create ? "Created" : "Updated")} label {label.Name}.".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        var unchanged = labels.Count - created - updated;
        var repository = store.Repository;
        await output.WriteLineAsync(
            (force
                ? string.Create(CultureInfo.InvariantCulture, $"{repository}: {created} created, {updated} updated, and {unchanged} unchanged labels.")
                : string.Create(CultureInfo.InvariantCulture, $"preview: {repository} would get {created} created, {updated} updated, and {unchanged} unchanged labels; --force writes them.")).AsMemory(),
            cancellationToken).ConfigureAwait(false);
    }

    // Forges store colors in lowercase and an absent description as empty.
    private static bool IsCurrent(RepositoryLabel label, RepositoryLabel current) =>
        string.Equals(label.Name, current.Name, StringComparison.Ordinal)
        && string.Equals(label.Color, current.Color, StringComparison.OrdinalIgnoreCase)
        && string.Equals(label.Description, current.Description, StringComparison.Ordinal);
}
