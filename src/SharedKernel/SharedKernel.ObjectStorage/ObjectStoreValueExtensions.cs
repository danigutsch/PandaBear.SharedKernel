namespace PandaBear.ObjectStorage;

/// <summary>Adapts validated keys to existing provider string contracts.</summary>
public static class ObjectStoreValueExtensions
{
    /// <summary>Calls <see cref="IObjectStore.OpenRead" /> with a validated key.</summary>
    /// <param name="store">The object storage provider.</param>
    /// <param name="objectKey">The validated key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The provider result.</returns>
    /// <exception cref="ArgumentNullException">The provider is null.</exception>
    /// <exception cref="InvalidOperationException">The key has not been initialized.</exception>
    public static ValueTask<ObjectReadResult> OpenRead(this IObjectStore store, ObjectStorageKey objectKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.OpenRead(objectKey.Value, ct);
    }

    /// <summary>Calls <see cref="IObjectStore.Exists" /> with a validated key.</summary>
    /// <param name="store">The object storage provider.</param>
    /// <param name="objectKey">The validated key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The provider result.</returns>
    /// <exception cref="ArgumentNullException">The provider is null.</exception>
    /// <exception cref="InvalidOperationException">The key has not been initialized.</exception>
    public static ValueTask<bool> Exists(this IObjectStore store, ObjectStorageKey objectKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.Exists(objectKey.Value, ct);
    }

    /// <summary>Calls <see cref="IObjectStore.Delete" /> with a validated key.</summary>
    /// <param name="store">The object storage provider.</param>
    /// <param name="objectKey">The validated key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The provider result.</returns>
    /// <exception cref="ArgumentNullException">The provider is null.</exception>
    /// <exception cref="InvalidOperationException">The key has not been initialized.</exception>
    public static ValueTask Delete(this IObjectStore store, ObjectStorageKey objectKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.Delete(objectKey.Value, ct);
    }

    /// <summary>Calls <see cref="IObjectStore.GetPublicUri" /> with a validated key.</summary>
    /// <param name="store">The object storage provider.</param>
    /// <param name="objectKey">The validated key.</param>
    /// <returns>The provider result.</returns>
    /// <exception cref="ArgumentNullException">The provider is null.</exception>
    /// <exception cref="InvalidOperationException">The key has not been initialized.</exception>
    public static Uri GetPublicUri(this IObjectStore store, ObjectStorageKey objectKey)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.GetPublicUri(objectKey.Value);
    }

    /// <summary>Calls <see cref="IObjectStore.ListKeys" /> with a validated prefix.</summary>
    /// <param name="store">The object storage provider.</param>
    /// <param name="prefix">The validated prefix.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The provider result.</returns>
    /// <exception cref="ArgumentNullException">The provider is null.</exception>
    public static ValueTask<IReadOnlyList<string>> ListKeys(this IObjectStore store, ObjectStoragePrefix prefix, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.ListKeys(prefix.Value, ct);
    }

    /// <summary>Calls <see cref="IObjectStore.ListObjects" /> with a validated prefix.</summary>
    /// <param name="store">The object storage provider.</param>
    /// <param name="prefix">The validated prefix.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The provider result.</returns>
    /// <exception cref="ArgumentNullException">The provider is null.</exception>
    public static ValueTask<IReadOnlyList<ObjectInventoryItem>> ListObjects(this IObjectStore store, ObjectStoragePrefix prefix, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.ListObjects(prefix.Value, ct);
    }
}
