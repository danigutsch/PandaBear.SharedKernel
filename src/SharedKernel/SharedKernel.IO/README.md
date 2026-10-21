# PandaBear.SharedKernel.IO

A dependency-free bounded seekable copy for multi-pass uploads and imports. Targets .NET 10.

```csharp
using PandaBear.IO;

await using var lease = await SeekableContent.Create(input, maxLength, memoryThreshold, ct);
await Validate(lease.Content, ct);
lease.Content.Position = 0;
await Store(lease.Content, ct);
```

Input remains caller-owned and open, consumed from its current position. The lease owns its copy.
Dispose it after every consumer finishes. Use sequentially; rewind explicitly between passes.
Content above the configured memory threshold spills to a private temporary file deleted on disposal.
Actual received bytes enforce the maximum; excess throws InvalidDataException. Empty input is valid.
Cancellation and copy failures clean up partial copies. Negative limits and unreadable input fail validation.

The threshold bounds retained content, not all process allocations. Hosts own request admission,
private temporary storage, disk quotas, and crash cleanup. Do not log input or temporary file paths.
NativeAOT analyzers are enabled; packed NativeAOT and Windows execution are not yet proven for this package.

See [stream intake policy](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/stream-intake.md)
for ownership, limits, memory-map decisions, benchmark evidence, and application adoption boundaries.
