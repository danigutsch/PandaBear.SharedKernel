# SharedKernel.Testing.ObjectStorage

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

In-memory `IObjectStore` for SharedKernel test suites, with call recording and failure injection.

## Usage

```csharp
using SharedKernel.ObjectStorage;
using SharedKernel.Testing.ObjectStorage;

var store = new InMemoryObjectStore { PublicBaseUri = new Uri("https://cdn.example/") };
store.Seed("uploads/original.jpg", originalBytes, "image/jpeg");
store.FailNext(ObjectStoreOperation.Delete, new IOException("delete failed"), "media/old.jpg");

await handler.Handle(notification, ct);

store.ObjectKeys.ShouldContain("media/v1/640.jpg");
store.Puts.ShouldAllSatisfy(put => put.StartPosition.ShouldBe(0));
store.CallCount(ObjectStoreOperation.Delete).ShouldBe(1);
```

## Contract

`InMemoryObjectStore` is thread-safe and keeps the contract of the S3 provider in `SharedKernel.ObjectStorage.S3`:

- Keys must pass `ObjectStorageKey.TryFrom` within `MaxObjectKeyLength`, and prefixes `ObjectStoragePrefix.TryFrom`; otherwise the
  call throws `ArgumentException`. A `null` prefix lists everything.
- `OpenRead` of a missing key throws `FileNotFoundException` whose `FileName` is the key. Each read returns its own read-only stream.
- `Delete` of a missing key completes. `ListKeys` and `ListObjects` return keys that start with the exact prefix, in ordinal order.
- `Put` reads the content from its current position and throws `ArgumentException` when the bytes read differ from the declared
  `Length`. The result carries the escaped key under `StorageBaseUri` and `PublicBaseUri`; `GetPublicUri` uses the same rule and
  accepts a relative base.
- `CreateUploadUrl` rejects the requests the S3 provider rejects and returns a ticket for the storage URI that expires
  `ExpiresAfter` from now and requires `Content-Type` and `Content-Length`. The store accepts no uploads; call `Put` to simulate one.
- Every asynchronous member returns a faulted or cancelled task rather than throwing, and checks the token before it records the call.
- Modification times and ticket expiry come from `TimeProvider`; pass a fake provider for deterministic times. `SetLastModified`
  overrides the time of one object.

`DiscardWrites` drains each write without storing it or allocating a buffer the size of the content. `WrapReadContent` replaces
the stream that each read hands out, for example with a stream that tracks disposal or stalls.

## Recording

A call is recorded after its arguments pass validation and before an injected failure is thrown.

| Member | Contains |
| --- | --- |
| `Calls`, `CallCount(operation)` | Every recorded call with its operation and key or prefix, including calls that threw an injected failure |
| `Puts` | Completed writes: key, content type, declared length, checksum, metadata, the stream's start position, writability and type, the bytes read, and the content (empty when discarded) |
| `Reads` | The `ObjectReadResult` instances `OpenRead` returned |
| `Deletes` | Keys of completed deletes, including keys that held no object |
| `ObjectKeys`, `FindObject(key)` | The stored keys, and an object's content, content type, checksum, metadata and modification time |

Each property returns a snapshot. `Seed` stores an object without recording a call or applying failures.

## Failure injection

`FailNext(operation, exception, target)` throws the exception from the next matching call; `FailAlways` throws it from every
matching call until `ClearFailures`. `target` limits the failure to one key, or one prefix for listings. The first registered
matching failure wins. A failed `Put` throws before it reads the content, so it neither stores nor consumes it. A failure on
`GetPublicUri` throws synchronously.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.ObjectStorage`. C# namespace: `SharedKernel.Testing.ObjectStorage`. Assembly/project identity: `SharedKernel.Testing.ObjectStorage`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
