# PandaBear.SharedKernel.IO

A dependency-free bounded seekable lease for multi-pass uploads and imports. Targets .NET 10.

```csharp
using PandaBear.IO;

await using var lease = await SeekableContent.Create(input, maxLength, memoryThreshold, ct);
await Validate(lease.Content, ct);
lease.Content.Position = 0;
await Store(lease.Content, ct);
```

Input remains caller-owned and open. A seekable input at position zero (such as `IFormFile.OpenReadStream()`) is
counted once with a pooled buffer, then returned uncopied as `Content`; the lease never disposes it, and its Position is
shared mutable state, so do not use the source concurrently or after the lease. Other input is copied from its current
position and the lease owns the copy. Dispose the lease after every consumer finishes. Use sequentially; rewind between passes.
Copied content above the configured memory threshold spills to a private temporary file deleted on disposal.
Actual received bytes enforce the maximum; excess throws InvalidDataException. Empty input is valid.
Cancellation and copy failures clean up partial copies and restore a passed-through
source to position zero. Negative limits and unreadable input fail validation.

The threshold bounds retained content, not all process allocations. Hosts own request admission,
private temporary storage, disk quotas, and crash cleanup. Do not log input or temporary file paths.
NativeAOT analyzers are enabled; packed NativeAOT and Windows execution are not yet proven for this package.

To hand bytes you already hold to a `Stream` API without copying, use .NET's `System.IO.ReadOnlyMemoryStream`, which
replaced this package's type of the same name in .NET 11.

## Atomic streaming writes

`AtomicFile.CreateIfAbsent` publishes a new file and returns whether this call won publication.
`AtomicFile.Replace` publishes over any previous contents. Both take a callback that can stream and
verify content before publication. Create the destination directory before calling either method.
On Unix, create-if-absent publication requires a filesystem that supports hard links.
If an existing file inventory reserves a temporary name prefix, pass that prefix to the
`Replace` overload. Temporary names consist of the prefix, a 32-digit hexadecimal identifier,
and `.tmp`.
For text files containing secrets, use `SecureFile.WriteAllText` for owner-only Unix permissions.

## Secret files and path identity

`SecureFile.WriteAllText` writes text that should stay private, such as a decrypted export: on Unix the file is readable and
writable only by its owner. It writes a temporary file in the same directory and moves it into place, so an interrupted write
leaves no partial file and a replaced file never keeps wider permissions; a canceled or failed write deletes the temporary
file. Without `overwrite` an existing file is refused.

`FilePaths.ResolveLinks` returns a path with every symbolic link in it resolved, component by component as the operating
system follows them, and `FilePaths.MayNameSameFile` tells whether two paths may reach the same file, ignoring case because a
case-sensitive system can mount a case-insensitive volume. Use it to refuse writing an output over one of the inputs.

```csharp
using PandaBear.IO;

if (FilePaths.MayNameSameFile(outputPath, inputPath))
{
    throw new InvalidOperationException("The output would overwrite the input.");
}

SecureFile.WriteAllText(outputPath, report, overwrite: false);
```

## Repository paths

`RepositoryPath` is a path that a configuration file names relative to a repository root: `/` separators, no root,
no `\` or `:`, and no empty, `.`, or `..` segment. `IsValid` and `TryParse` check text; `Parse` throws
`InvalidOperationException` for invalid text, because configuration readers validate first. `ResolveUnder(root)`
combines the path with the root and refuses a symbolic link in the path or any of its parent directories, so a
checkout cannot point a configured path outside itself.

```csharp
if (!RepositoryPath.TryParse(configuredScript, out var script))
{
    return Invalid("$.script must be repository-relative.");
}

var fullPath = script.ResolveUnder(root);
```

## Bounded whole reads

`BoundedReader.ReadFile(path, maxBytes, ct)` and `BoundedReader.ReadToEnd(stream, maxBytes, ct)` read small inputs, such
as configuration files, reports, and API responses, whole into a `byte[]`, and return `null` as soon as the input holds
more than `maxBytes` bytes, also when a file grows while it is read. The caller words the size error. A seekable stream
whose remaining length is already over the limit is refused without reading. `ReadFile` lets other processes write,
rename, or delete the file during the read and lets `FileNotFoundException`, `DirectoryNotFoundException`, and other
I/O errors propagate. `BoundedReader.ReadContent(content, maxBytes, ct)` reads an `HttpContent`, such as a response body,
the same way: a declared `Content-Length` over the limit is refused without reading.

The optional `PandaBear.SharedKernel.IO.Analyzers` package flags unbounded buffering of request and upload
streams (`SKIO001`) and hand-rolled copy loops into a `MemoryStream` (`SKIO002`) and points to this API.

See [stream intake policy](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/stream-intake.md)
for ownership, limits, memory-map decisions, benchmark evidence, and application adoption boundaries.

## Reviewed manifests and artifacts

`ArtifactManifest.Read` deserializes a bounded manifest using caller-supplied `JsonTypeInfo` and
returns the SHA-256 of the exact file bytes. Select strict JSON options and validate the parsed model
in the consumer. `VerifiedArtifact.Read` returns bounded bytes that matched an expected SHA-256.
`VerifiedArtifact.Verify` checks a digest without retaining the file contents. Keep the artifact bundle
in a caller-controlled directory. The consumer owns migration phases, release policy, SQL validation,
and execution.
