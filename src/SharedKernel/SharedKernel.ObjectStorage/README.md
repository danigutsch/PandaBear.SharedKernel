# SharedKernel.ObjectStorage

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Host-neutral object-storage contracts.

`IObjectStore` writes, reads, lists, deletes, and signs direct uploads for application-owned, relative,
slash-delimited object keys. It carries no application key policy, variants, or publishing rules.

Provider implementations belong in separate packages. The S3-compatible provider is
`SharedKernel.ObjectStorage.S3`.

## Validated object identities

Use `ObjectStorageKey.From` or `TryFrom` to validate relative slash-delimited identities before passing
values between components. Keys reject rooted paths, empty and dot segments, backslashes, control
characters, and malformed Unicode. They retain exact case, spaces, Unicode, and literal percent
characters; they never trim or decode input. `ToEscapedPath` escapes segments independently for URLs.
An uninitialized key cannot be read. Supply the application's or provider's length limit explicitly.

`ObjectStoragePrefix` preserves exact listing semantics, including partial segments and trailing
slashes. Its empty/default value lists every object. A prefix is not interchangeable with an object
key. Provider length limits and filesystem containment checks still apply at the storage boundary.

```csharp
using PandaBear.ObjectStorage;

var key = ObjectStorageKey.From("media/original.jpg", maxLength: 1024);
await store.Exists(key, ct);
await store.ListKeys(ObjectStoragePrefix.From("media/"), ct);
```

Typed overloads adapt to existing `IObjectStore` string contracts, so existing provider implementations
remain compatible. Write and upload request `Create` factories also accept typed keys. This package reuses
the existing `PandaBear.SharedKernel.InputNormalization` package for validation; no additional
third-party dependency is introduced.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.ObjectStorage`. C# namespace: `PandaBear.ObjectStorage`. Assembly/project identity: `SharedKernel.ObjectStorage`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
