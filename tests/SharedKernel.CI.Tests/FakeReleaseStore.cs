namespace SharedKernel.CI.Tests;

/// <summary>Holds releases in memory and fails the deletion of chosen releases with chosen exceptions.</summary>
internal sealed class FakeReleaseStore(params RepositoryRelease[] releases) : IReleaseStore
{
    /// <summary>Gets the failures by release ID.</summary>
    public Dictionary<long, Exception> Failures { get; } = [];

    /// <summary>Gets the IDs of the deleted releases, in order.</summary>
    public List<long> Deleted { get; } = [];

    /// <inheritdoc />
    public Task<IReadOnlyList<RepositoryRelease>> List(CancellationToken ct) => Task.FromResult<IReadOnlyList<RepositoryRelease>>(releases);

    /// <inheritdoc />
    public Task Delete(RepositoryRelease release, CancellationToken ct)
    {
        if (Failures.TryGetValue(release.Id, out var failure))
        {
            return Task.FromException(failure);
        }

        Deleted.Add(release.Id);
        return Task.CompletedTask;
    }
}
