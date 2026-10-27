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

### Limits

The analysis is local: it does not follow parameters, fields, reassigned locals, or other methods, and a
`Length` or `ContentLength` check is a hint, not proof that received bytes are counted. A request size limit
configured by the host (Kestrel, `RequestSizeLimit`) is not visible; lower the severity or suppress where
the host enforces it. Prefer `SeekableContent.Create`, which counts received bytes.

## Configuration

```ini
dotnet_diagnostic.SKIO001.severity = warning
dotnet_diagnostic.SKIO002.severity = warning
```

## See Also

- [SharedKernel.IO](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.IO/README.md)
- [Stream intake policy](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/stream-intake.md)

## Package identity and availability

Package ID: `PandaBear.SharedKernel.IO.Analyzers`. C# namespace: `PandaBear.IO.Analyzers`. Assembly/project identity: `SharedKernel.IO.Analyzers`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
