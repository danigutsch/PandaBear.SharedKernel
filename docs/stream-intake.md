# File and stream intake

Use one forward streaming pass when validation and the destination allow it. Keep request cancellation,
size limits, content validation, malware checks, and authorization in the host. Reject oversized input
while reading; Content-Length and Stream.Length are hints, not substitutes for counting received bytes.
Do not log payloads, original filenames, temporary paths, or upload credentials.

Buffer only bounded small content. Spool when a non-seekable input needs independent sequential passes
such as signature validation, scanning, decoding, hashing, and storage. Rewind explicitly before each
pass. Do not duplicate byte arrays or add a second copy merely because an API accepts ReadOnlyMemory.
Wrap bytes already in memory with `ReadOnlyMemoryStream` instead of copying them into a `MemoryStream`.
Use seekable input directly only when the caller controls its lifetime, size, position, and concurrent use.

Memory mapping is for measured random access to an existing local file whose lifetime is controlled.
A network request is not a local file and cannot be mapped directly. Creating a file solely to map a
sequential upload adds I/O and lifetime complexity. Prefer ordinary stream reads unless measurements
of the actual workload justify mapping. Do not bypass malware scanning or validation with a faster path.

## API decision

Add one concrete `PandaBear.IO.SeekableContent` lease in `PandaBear.SharedKernel.IO`, with no runtime
package dependencies. `Create(Stream source, long maxLength, int memoryThreshold, CancellationToken ct)`
copies from the current position, leaves source open, and returns `Content` positioned at zero.
The lease owns only the copy. Dispose it after all passes. The stream is mutable, intended for sequential
use by cooperating callers, and must not escape the lease lifetime. No interfaces, DI registrations,
factories, extension surfaces, caching, or custom exception hierarchy are needed.

`maxLength` counts actual copied bytes; zero accepts only empty input. Oversize input throws
`InvalidDataException` after at most one extra byte is consumed. Negative limits fail before reads.
Empty input is valid here; minimum lengths and file formats are host policy. Copy failures and cancellation
clean up the partial copy; they never dispose or rewind source. Retrying requires a new source or caller rewind.

The memory threshold bounds content retained in memory, not total process allocations: the fixed copy
buffer, stream bookkeeping, and transient memory during growth also count. Spill before writing a chunk
that would cross the threshold. Temporary files use random names, exclusive CreateNew, no sharing,
owner-only Unix permissions, and DeleteOnClose. Disposal is required, including failure paths. Abrupt host
termination and operating-system failures are outside the lease's guaranteed cleanup boundary; hosts own
private temp-volume provisioning, disk quotas, admission limits, and crash recovery. Avoid untrusted temp
directories. Do not infer an application-wide threshold from one developer-machine result.

## Current caller and adoption boundary

`ViajantesTurismo.Catalog.Application/Media/MediaImageUploadIntake.Accept` currently copies into a
MemoryStream, then validates the header, scans, decodes, hashes, and stores the content. Its existing
`CopyToMemory` enforces maximum length but retains the complete accepted payload in managed memory.
That is the concrete multi-pass use case. Customer CSV imports that can remain single-pass should keep
streaming. Application adoption and its compatibility tests remain in ViajantesTurismo#705.

A consuming upload/import flow uses:

```csharp
await using var lease = await SeekableContent.Create(input, maxLength, memoryThreshold, ct);
var content = lease.Content;
// Validate bounded header, scan, decode, hash, and store sequentially.
// Set content.Position = 0 before each new pass.
```

Translate oversize failures at the application boundary to its existing validation result. Preserve
actual-length comparison, minimum length, scanning status, decoded image limits, transaction ordering,
and storage compensation. Helpers accepting MemoryStream specifically need explicit Stream migration
and application tests; this package does not silently change them.

## Analyzer enforcement

The optional `PandaBear.SharedKernel.IO.Analyzers` package reports `SKIO001` when `Stream.CopyTo[Async]`
into a `MemoryStream` or `StreamReader.ReadToEnd[Async]` consumes `IFormFile.OpenReadStream()`,
`HttpRequest.Body`, or `HttpContent.ReadAsStream[Async]`, and when `HttpContent.ReadAsByteArrayAsync` is
called, without a `Length` or `ContentLength` comparison in the same member. It reports `SKIO002` for a loop
that reads a stream and writes each chunk into a `MemoryStream` without a non-zero relational comparison
(a running total against a maximum). Both steer to `SeekableContent.Create`. A host-owned request size
limit is not visible to the analyzer; suppress or configure severity where the host enforces it.
See the [package README](../src/SharedKernel/SharedKernel.IO.Analyzers/README.md).

## Evidence and verification

The real HTTP comparison and its limitations live in
[the IO benchmark](../benchmarks/SharedKernel.IO.Benchmarks/README.md). Focused lease tests cover
non-seekable input, exact and exceeded limits, cancellation, and cleanup. No claim of packed NativeAOT
execution or Windows execution follows from analyzer compatibility alone.
