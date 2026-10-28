# SharedKernel.Testing.Logging

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Recording and throwing `ILogger` fakes for SharedKernel test suites.

## Usage

```csharp
using PandaBear.Testing.Logging;

var logger = new RecordingLogger<MyService>();
new MyService(logger).Run();
logger.Records.ShouldContain(record => record.Level == LogLevel.Warning);
```

`RecordingLogger` keeps level, event ID, rendered message, structured state, and exception for every call. `ThrowingLogger<T>` throws `InvalidOperationException` with `ThrowingLogger<T>.Secret` from every call, to prove telemetry never breaks the caller or leaks the message.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Logging`. C# namespace: `PandaBear.Testing.Logging`. Assembly/project identity: `SharedKernel.Testing.Logging`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
