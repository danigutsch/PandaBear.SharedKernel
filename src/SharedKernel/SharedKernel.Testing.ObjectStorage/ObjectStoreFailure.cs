namespace SharedKernel.Testing.ObjectStorage;

/// <summary>A failure registered on <see cref="InMemoryObjectStore" />.</summary>
/// <param name="Operation">The operation that fails.</param>
/// <param name="Target">The key or prefix the call must name, or <see langword="null" /> for every call.</param>
/// <param name="Exception">The exception to throw.</param>
/// <param name="Once">Whether the failure is removed after it is thrown once.</param>
internal sealed record ObjectStoreFailure(ObjectStoreOperation Operation, string? Target, Exception Exception, bool Once)
{
    /// <summary>Returns whether this failure applies to a call.</summary>
    /// <param name="operation">The called operation.</param>
    /// <param name="target">The key or prefix the call named.</param>
    /// <returns><see langword="true" /> when the failure applies.</returns>
    public bool Matches(ObjectStoreOperation operation, string target) =>
        Operation == operation && (Target is null || string.Equals(Target, target, StringComparison.Ordinal));
}
