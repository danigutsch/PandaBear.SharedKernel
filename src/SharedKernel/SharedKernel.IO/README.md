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

The optional `PandaBear.SharedKernel.IO.Analyzers` package flags unbounded buffering of request and upload
streams (`SKIO001`) and hand-rolled copy loops into a `MemoryStream` (`SKIO002`) and points to this API.

See [stream intake policy](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/stream-intake.md)
for ownership, limits, memory-map decisions, benchmark evidence, and application adoption boundaries.
