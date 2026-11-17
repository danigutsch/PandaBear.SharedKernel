using System.Buffers;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using PandaBear.ObjectStorage;

namespace PandaBear.Testing.ObjectStorage;

/// <summary>
/// Thread-safe in-memory <see cref="IObjectStore" /> that keeps the argument, missing-object, listing and upload-ticket contract of
/// the S3 provider, records the calls it receives, and throws injected failures.
/// </summary>
/// <remarks>
/// Every member validates its arguments before it records the call, so a call with an invalid key, prefix or upload request, or a
/// cancelled token, throws without being recorded. A recorded call then throws the first registered failure that matches it.
/// Listings are ordered by ordinal key. Times come from <see cref="TimeProvider" />.
/// </remarks>
public sealed class InMemoryObjectStore : IObjectStore
{
    private const int CopyBufferSize = 81_920;
    private static readonly TimeSpan MaxUploadLifetime = TimeSpan.FromDays(7);
    private static readonly ReadOnlyDictionary<string, string> NoMetadata = ReadOnlyDictionary<string, string>.Empty;

    private readonly Lock gate = new();
    private readonly Dictionary<string, StoredObject> objects = new(StringComparer.Ordinal);
    private readonly List<ObjectStoreCall> calls = [];
    private readonly List<RecordedObjectPut> puts = [];
    private readonly List<ObjectReadResult> reads = [];
    private readonly List<string> deletes = [];
    private readonly List<ObjectStoreFailure> failures = [];

    private readonly Uri publicBaseUri = new("https://public.invalid/");
    private readonly Uri storageBaseUri = new("https://storage.invalid/");
    private readonly TimeProvider timeProvider = TimeProvider.System;
    private readonly int maxObjectKeyLength = int.MaxValue;

    /// <summary>Gets the base of the URIs that <see cref="GetPublicUri" /> returns; absolute or relative. Defaults to <c>https://public.invalid/</c>.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null" />.</exception>
    public Uri PublicBaseUri
    {
        get => publicBaseUri;
        init => publicBaseUri = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets the absolute base of storage URIs in write results and of upload ticket URIs. Defaults to <c>https://storage.invalid/</c>.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The value is not absolute.</exception>
    public Uri StorageBaseUri
    {
        get => storageBaseUri;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            storageBaseUri = value.IsAbsoluteUri ? value : throw new ArgumentException("The storage base URI must be absolute.", nameof(value));
        }
    }

    /// <summary>Gets the clock for modification times and upload ticket expiry. Defaults to <see cref="TimeProvider.System" />.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null" />.</exception>
    public TimeProvider TimeProvider
    {
        get => timeProvider;
        init => timeProvider = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets the longest object key the store accepts, as the provider length limit. Defaults to no limit.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int MaxObjectKeyLength
    {
        get => maxObjectKeyLength;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            maxObjectKeyLength = value;
        }
    }

    /// <summary>
    /// Gets whether writes drain their content without keeping it. A discarded write is recorded in <see cref="Puts" /> with its
    /// <see cref="RecordedObjectPut.BytesRead" /> and empty <see cref="RecordedObjectPut.Content" />, stores nothing, and allocates
    /// no buffer the size of the content.
    /// </summary>
    public bool DiscardWrites { get; init; }

    /// <summary>
    /// Gets an optional function that receives the object key and the content stream of each read and returns the stream that
    /// <see cref="OpenRead" /> hands out, for example a stream that tracks disposal or stalls.
    /// </summary>
    public Func<string, Stream, Stream>? WrapReadContent { get; init; }

    /// <summary>Gets a snapshot of the stored object keys in ordinal order.</summary>
    public IReadOnlyList<string> ObjectKeys
    {
        get
        {
            lock (gate)
            {
                return [.. objects.Keys.Order(StringComparer.Ordinal)];
            }
        }
    }

    /// <summary>Gets a snapshot of every recorded call in order, including calls that threw an injected failure.</summary>
    public IReadOnlyList<ObjectStoreCall> Calls
    {
        get
        {
            lock (gate)
            {
                return [.. calls];
            }
        }
    }

    /// <summary>Gets a snapshot of the completed writes in order.</summary>
    public IReadOnlyList<RecordedObjectPut> Puts
    {
        get
        {
            lock (gate)
            {
                return [.. puts];
            }
        }
    }

    /// <summary>Gets a snapshot of the results <see cref="OpenRead" /> returned, in order.</summary>
    public IReadOnlyList<ObjectReadResult> Reads
    {
        get
        {
            lock (gate)
            {
                return [.. reads];
            }
        }
    }

    /// <summary>Gets a snapshot of the keys of completed deletes in order, including keys that held no object.</summary>
    public IReadOnlyList<string> Deletes
    {
        get
        {
            lock (gate)
            {
                return [.. deletes];
            }
        }
    }

    /// <summary>Counts the recorded calls of an operation, including calls that threw an injected failure.</summary>
    /// <param name="operation">The operation to count.</param>
    /// <returns>The number of recorded calls.</returns>
    public int CallCount(ObjectStoreOperation operation)
    {
        lock (gate)
        {
            return calls.Count(call => call.Operation == operation);
        }
    }

    /// <summary>Returns the object stored under a key.</summary>
    /// <param name="objectKey">The object key.</param>
    /// <returns>The stored object, or <see langword="null" /> when none is stored.</returns>
    /// <exception cref="ArgumentException">The key is invalid.</exception>
    public StoredObject? FindObject(string objectKey)
    {
        ValidateKey(objectKey);
        lock (gate)
        {
            return objects.GetValueOrDefault(objectKey);
        }
    }

    /// <summary>Stores an object without recording a call or applying failures, for arranging a test.</summary>
    /// <param name="objectKey">The object key.</param>
    /// <param name="content">The bytes to store; copied.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="checksum">The optional checksum.</param>
    /// <param name="metadata">The optional metadata; copied.</param>
    /// <exception cref="ArgumentException">The key is invalid or the content type is blank.</exception>
    public void Seed(string objectKey, ReadOnlySpan<byte> content, string contentType, string? checksum = null, IReadOnlyDictionary<string, string>? metadata = null)
    {
        ValidateKey(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        var stored = new StoredObject(objectKey, content.ToArray(), contentType, checksum, CopyMetadata(metadata) ?? NoMetadata, timeProvider.GetUtcNow());
        lock (gate)
        {
            objects[objectKey] = stored;
        }
    }

    /// <summary>Overrides the modification time of a stored object, for age-based scenarios.</summary>
    /// <param name="objectKey">The object key.</param>
    /// <param name="lastModifiedAt">The modification time to report.</param>
    /// <exception cref="ArgumentException">The key is invalid.</exception>
    /// <exception cref="KeyNotFoundException">No object is stored under the key.</exception>
    public void SetLastModified(string objectKey, DateTimeOffset lastModifiedAt)
    {
        ValidateKey(objectKey);
        lock (gate)
        {
            if (!objects.TryGetValue(objectKey, out var stored))
            {
                throw new KeyNotFoundException($"No object is stored under '{objectKey}'.");
            }

            objects[objectKey] = stored with { LastModifiedAt = lastModifiedAt };
        }
    }

    /// <summary>Makes the next matching call of an operation throw an exception, once.</summary>
    /// <param name="operation">The operation that fails.</param>
    /// <param name="exception">The exception to throw.</param>
    /// <param name="target">The key, or the prefix for listings, the call must name; <see langword="null" /> matches every call.</param>
    /// <exception cref="ArgumentNullException">The exception is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The operation is undefined.</exception>
    public void FailNext(ObjectStoreOperation operation, Exception exception, string? target = null) =>
        AddFailure(operation, exception, target, once: true);

    /// <summary>Makes every matching call of an operation throw an exception until <see cref="ClearFailures" />.</summary>
    /// <param name="operation">The operation that fails.</param>
    /// <param name="exception">The exception to throw; the same instance is thrown each time.</param>
    /// <param name="target">The key, or the prefix for listings, the call must name; <see langword="null" /> matches every call.</param>
    /// <exception cref="ArgumentNullException">The exception is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The operation is undefined.</exception>
    public void FailAlways(ObjectStoreOperation operation, Exception exception, string? target = null) =>
        AddFailure(operation, exception, target, once: false);

    /// <summary>Removes every registered failure.</summary>
    public void ClearFailures()
    {
        lock (gate)
        {
            failures.Clear();
        }
    }

    /// <summary>
    /// Reads the content from its current position and stores it, or drains it when <see cref="DiscardWrites" /> is set. An
    /// injected failure throws before the content is read.
    /// </summary>
    /// <param name="request">The write request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The stored object result with storage and public URIs.</returns>
    /// <exception cref="ArgumentNullException">The request is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The key is invalid, or the content length differs from the declared length.</exception>
    public async ValueTask<ObjectWriteResult> Put(ObjectWriteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        ValidateKey(request.ObjectKey);
        Begin(ObjectStoreOperation.Put, request.ObjectKey);

        var source = request.Content;
        long? startPosition = source.CanSeek ? source.Position : null;
        var canWrite = source.CanWrite;
        var streamType = source.GetType();
        byte[] content;
        long bytesRead;
        if (DiscardWrites)
        {
            bytesRead = await Drain(source, destination: null, ct).ConfigureAwait(false);
            content = [];
        }
        else
        {
            using var buffer = new MemoryStream();
            bytesRead = await Drain(source, buffer, ct).ConfigureAwait(false);
            content = buffer.ToArray();
        }

        if (bytesRead != request.Length)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"The content held {bytesRead} bytes but the request declared {request.Length}."),
                nameof(request));
        }

        var metadata = CopyMetadata(request.Metadata);
        var put = new RecordedObjectPut(
            request.ObjectKey, request.ContentType, request.Length, request.Checksum, metadata, startPosition, canWrite, streamType, bytesRead, content);
        lock (gate)
        {
            puts.Add(put);
            if (!DiscardWrites)
            {
                objects[request.ObjectKey] = new StoredObject(
                    request.ObjectKey, content, request.ContentType, request.Checksum, metadata ?? NoMetadata, timeProvider.GetUtcNow());
            }
        }

        return new ObjectWriteResult(request.ObjectKey, CreateStorageUri(request.ObjectKey), CreatePublicUri(request.ObjectKey), request.ContentType, bytesRead, request.Checksum);
    }

    /// <summary>Opens a read-only copy of a stored object, passed through <see cref="WrapReadContent" /> when it is set.</summary>
    /// <param name="objectKey">The object key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The object content and metadata.</returns>
    /// <exception cref="ArgumentException">The key is invalid.</exception>
    /// <exception cref="FileNotFoundException">No object is stored under the key; <see cref="FileNotFoundException.FileName" /> is the key.</exception>
    public ValueTask<ObjectReadResult> OpenRead(string objectKey, CancellationToken ct) => Complete(() =>
    {
        ct.ThrowIfCancellationRequested();
        ValidateKey(objectKey);
        Begin(ObjectStoreOperation.OpenRead, objectKey);

        StoredObject? stored;
        lock (gate)
        {
            stored = objects.GetValueOrDefault(objectKey);
        }

        if (stored is null)
        {
            throw new FileNotFoundException("The object is unavailable.", objectKey);
        }

        Stream content = MemoryMarshal.TryGetArray(stored.Content, out var segment)
            ? new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(stored.Content.ToArray(), writable: false);
        if (WrapReadContent is { } wrap)
        {
            content = wrap(objectKey, content) ?? throw new InvalidOperationException("WrapReadContent returned null.");
        }

        var result = new ObjectReadResult(objectKey, content, stored.ContentType, stored.Length, stored.Checksum);
        lock (gate)
        {
            reads.Add(result);
        }

        return result;
    });

    /// <summary>Returns whether an object is stored under a key.</summary>
    /// <param name="objectKey">The object key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns><see langword="true" /> when the object exists.</returns>
    /// <exception cref="ArgumentException">The key is invalid.</exception>
    public ValueTask<bool> Exists(string objectKey, CancellationToken ct) => Complete(() =>
    {
        ct.ThrowIfCancellationRequested();
        ValidateKey(objectKey);
        Begin(ObjectStoreOperation.Exists, objectKey);
        lock (gate)
        {
            return objects.ContainsKey(objectKey);
        }
    });

    /// <summary>Lists the stored keys that start with the exact prefix, in ordinal order.</summary>
    /// <param name="prefix">The prefix; empty or <see langword="null" /> lists every key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching keys.</returns>
    /// <exception cref="ArgumentException">The prefix is invalid.</exception>
    public ValueTask<IReadOnlyList<string>> ListKeys(string prefix, CancellationToken ct) => Complete<IReadOnlyList<string>>(() =>
    {
        var listed = List(ObjectStoreOperation.ListKeys, prefix, ct);
        return [.. listed.Select(static item => item.ObjectKey)];
    });

    /// <summary>Lists the stored objects whose keys start with the exact prefix, in ordinal key order.</summary>
    /// <param name="prefix">The prefix; empty or <see langword="null" /> lists every object.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching objects with their modification times.</returns>
    /// <exception cref="ArgumentException">The prefix is invalid.</exception>
    public ValueTask<IReadOnlyList<ObjectInventoryItem>> ListObjects(string prefix, CancellationToken ct) => Complete<IReadOnlyList<ObjectInventoryItem>>(() =>
    {
        var listed = List(ObjectStoreOperation.ListObjects, prefix, ct);
        return [.. listed.Select(static item => new ObjectInventoryItem(item.ObjectKey, item.LastModifiedAt))];
    });

    /// <summary>Returns the key, with each segment escaped, under <see cref="PublicBaseUri" />.</summary>
    /// <param name="objectKey">The object key.</param>
    /// <returns>The public URI.</returns>
    /// <exception cref="ArgumentException">The key is invalid.</exception>
    public Uri GetPublicUri(string objectKey)
    {
        ValidateKey(objectKey);
        Begin(ObjectStoreOperation.GetPublicUri, objectKey);
        return CreatePublicUri(objectKey);
    }

    /// <summary>
    /// Returns an upload ticket for the storage URI of the key that expires after the requested lifetime and requires the
    /// <c>Content-Type</c> and <c>Content-Length</c> headers. The store does not accept uploads; call <see cref="Put" /> to simulate one.
    /// </summary>
    /// <param name="request">The upload request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The upload ticket.</returns>
    /// <exception cref="ArgumentNullException">The request is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The key is invalid, or the content type is blank or contains a line break.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The length is not positive, or the lifetime is not positive or exceeds seven days.</exception>
    public ValueTask<ObjectUploadTicket> CreateUploadUrl(ObjectUploadRequest request, CancellationToken ct) => Complete(() =>
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        ValidateKey(request.ObjectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ContentType);
        if (request.ContentType.Contains('\r', StringComparison.Ordinal) || request.ContentType.Contains('\n', StringComparison.Ordinal))
        {
            throw new ArgumentException("Content type must not contain newlines.", nameof(request));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Length);
        if (request.ExpiresAfter <= TimeSpan.Zero || request.ExpiresAfter > MaxUploadLifetime)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Upload lifetime must be positive and at most seven days.");
        }

        Begin(ObjectStoreOperation.CreateUploadUrl, request.ObjectKey);
        IReadOnlyDictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = request.ContentType,
            ["Content-Length"] = request.Length.ToString(CultureInfo.InvariantCulture)
        };
        return new ObjectUploadTicket(request.ObjectKey, CreateStorageUri(request.ObjectKey), timeProvider.GetUtcNow().Add(request.ExpiresAfter), headers);
    });

    /// <summary>Removes the object stored under a key; deleting a key that holds no object completes.</summary>
    /// <param name="objectKey">The object key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A completed operation.</returns>
    /// <exception cref="ArgumentException">The key is invalid.</exception>
    public ValueTask Delete(string objectKey, CancellationToken ct) => Complete(() =>
    {
        ct.ThrowIfCancellationRequested();
        ValidateKey(objectKey);
        Begin(ObjectStoreOperation.Delete, objectKey);
        lock (gate)
        {
            objects.Remove(objectKey);
            deletes.Add(objectKey);
        }
    });

    // Faults the returned task instead of throwing, as the asynchronous S3 provider does.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Every exception is returned through the faulted task.")]
    private static ValueTask<T> Complete<T>(Func<T> operation)
    {
        try
        {
            return ValueTask.FromResult(operation());
        }
        catch (OperationCanceledException exception) when (exception.CancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<T>(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            return ValueTask.FromException<T>(exception);
        }
    }

    // Faults the returned task instead of throwing, as the asynchronous S3 provider does.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Every exception is returned through the faulted task.")]
    private static ValueTask Complete(Action operation)
    {
        try
        {
            operation();
            return ValueTask.CompletedTask;
        }
        catch (OperationCanceledException exception) when (exception.CancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            return ValueTask.FromException(exception);
        }
    }

    private static async ValueTask<long> Drain(Stream source, MemoryStream? destination, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                if (destination is not null)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                }

                total += read;
            }

            return total;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static ReadOnlyDictionary<string, string>? CopyMetadata(IReadOnlyDictionary<string, string>? metadata) =>
        metadata is null ? null : new Dictionary<string, string>(metadata, StringComparer.Ordinal).AsReadOnly();

    private static Uri WithTrailingSlash(Uri baseUri) =>
        baseUri.OriginalString.EndsWith('/', StringComparison.Ordinal)
            ? baseUri
            : new Uri(baseUri.OriginalString + "/", baseUri.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative);

    private StoredObject[] List(ObjectStoreOperation operation, string? prefix, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        prefix ??= string.Empty;
        if (!ObjectStoragePrefix.TryFrom(prefix, out _, maxObjectKeyLength))
        {
            throw new ArgumentException("Object prefix must be relative and must not include dot segments.", nameof(prefix));
        }

        Begin(operation, prefix);
        lock (gate)
        {
            return [.. objects.Values
                .Where(item => item.ObjectKey.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(static item => item.ObjectKey, StringComparer.Ordinal)];
        }
    }

    private void AddFailure(ObjectStoreOperation operation, Exception exception, string? target, bool once)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation), operation, "The operation is undefined.");
        }

        lock (gate)
        {
            failures.Add(new ObjectStoreFailure(operation, target, exception, once));
        }
    }

    private void Begin(ObjectStoreOperation operation, string target)
    {
        Exception? failure = null;
        lock (gate)
        {
            calls.Add(new ObjectStoreCall(operation, target));
            var index = failures.FindIndex(candidate => candidate.Matches(operation, target));
            if (index >= 0)
            {
                failure = failures[index].Exception;
                if (failures[index].Once)
                {
                    failures.RemoveAt(index);
                }
            }
        }

        if (failure is not null)
        {
            throw failure;
        }
    }

    private void ValidateKey(string objectKey)
    {
        if (!ObjectStorageKey.TryFrom(objectKey, out _, maxObjectKeyLength))
        {
            throw new ArgumentException("Object key must be a relative slash-delimited path without dot segments.", nameof(objectKey));
        }
    }

    private Uri CreateStorageUri(string objectKey) => new(WithTrailingSlash(storageBaseUri), ObjectStorageKey.From(objectKey).ToEscapedPath());

    private Uri CreatePublicUri(string objectKey)
    {
        var baseUri = WithTrailingSlash(publicBaseUri);
        var escapedKey = ObjectStorageKey.From(objectKey).ToEscapedPath();
        return baseUri.IsAbsoluteUri ? new Uri(baseUri, escapedKey) : new Uri(baseUri.OriginalString + escapedKey, UriKind.Relative);
    }
}
