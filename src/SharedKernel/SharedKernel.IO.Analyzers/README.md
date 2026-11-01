# SharedKernel.IO.Analyzers

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Roslyn analyzers for upload and import stream handling. They steer unbounded buffering to
`PandaBear.IO.SeekableContent.Create` from `PandaBear.SharedKernel.IO`. The package has no runtime
dependency and does not require `PandaBear.SharedKernel.IO`.

## Diagnostics

| ID | Default severity | Purpose |
| --- | --- | --- |
| `SKIO001` | Warning | A request or upload stream is buffered in memory without an explicit length bound. |
| `SKIO002` | Warning | A hand-rolled read loop copies a stream into a `MemoryStream` without a length bound. |
| `SKIO003` | Disabled; opt-in warning | An upload or raw-body endpoint sets no endpoint-level request size limit. |

### SKIO001

Reported on:

- `Stream.CopyTo` or `CopyToAsync` into a `MemoryStream` when the source is `IFormFile.OpenReadStream()`,
  `HttpRequest.Body`, or `HttpContent.ReadAsStream[Async]`, directly or through a local initialized from one.
- `StreamReader.ReadToEnd` or `ReadToEndAsync` on a reader created over one of those streams.
- `HttpContent.ReadAsByteArrayAsync`.

Not reported when the enclosing member compares a `Length` or `ContentLength` member with a relational
operator (for example `file.Length > Max`), or when the source is a wrapper type such as a length-limited
stream, because only the listed intake sources are traced.

### SKIO002

Reported on a `while`, `do`, or `for` loop that calls `Stream.Read[Async]` and `MemoryStream.Write[Async]`
with no relational comparison other than against zero. A running total compared with a maximum, or
`remaining > 0`-style counters compared with a non-zero value, count as a bound.

### SKIO003

Disabled by default, because a limit configured by the host (Kestrel options, endpoint groups, filters) is not visible
to the analyzer. Enable it where upload endpoints are expected to carry their own size budget; Kestrel's default is
30,000,000 bytes.

Reported on an endpoint that takes an `IFormFile`, `IFormFileCollection`, `PipeReader` or `Stream` parameter, or reads
`HttpRequest.Body`:

- a public instance action of a `ControllerBase` type, and
- the handler of a minimal-API `MapPost`, `MapPut`, `MapPatch`, `MapMethods` or `Map` call (a lambda or a method group).

Not reported when the endpoint has a limit:

- `[RequestSizeLimit]` or `[DisableRequestSizeLimit]` on the action, its controller type, or the handler (a lambda
  attribute or the handler method);
- `[RequestFormLimits]` that sets `MultipartBodyLengthLimit`; other `RequestFormLimits` settings do not bound the upload;
- a later call in the `Map*` chain that passes a `RequestSizeLimitAttribute` or an `IRequestSizeLimitMetadata`, for
  example `.WithMetadata(new RequestSizeLimitAttribute(10_000_000))`;
- a reference to `IHttpMaxRequestBodySizeFeature` in the endpoint body.

Non-endpoint methods, `MapGet`, and handlers that take none of the listed parameters and do not read `Request.Body`
are not checked. A method-group handler is checked by its parameters and attributes only, not its body.

There is no code fix: the maximum size, its unit, and whether to disable the limit are application policy, so a fix
could only insert an arbitrary number.

### Limits

The analysis is local: it does not follow parameters, fields, reassigned locals, or other methods, and a
`Length` or `ContentLength` check is a hint, not proof that received bytes are counted. A request size limit
configured by the host (Kestrel, `RequestSizeLimit`) is not visible; lower the severity or suppress where
the host enforces it. Prefer `SeekableContent.Create`, which counts received bytes.

## Configuration

```ini
dotnet_diagnostic.SKIO001.severity = warning
dotnet_diagnostic.SKIO002.severity = warning
dotnet_diagnostic.SKIO003.severity = warning
```

## See Also

- [SharedKernel.IO](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.IO/README.md)
- [Stream intake policy](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/stream-intake.md)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.IO.Analyzers`. C# namespace: `PandaBear.IO.Analyzers`. Assembly/project identity: `SharedKernel.IO.Analyzers`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
