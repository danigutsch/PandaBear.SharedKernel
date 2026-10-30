# SharedKernel.Testing.Domain

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Domain event dispatcher and dispatch handler fakes for SharedKernel test suites.

## Usage

```csharp
using PandaBear.Testing.Domain;

var dispatcher = new RecordingDomainEventDispatcher();
await publisher.Publish(aggregate, dispatcher, ct);
dispatcher.Dispatched.ShouldHaveSingleItem();
```

`RecordingDomainEventDispatcher` stores dispatched events instead of publishing them. `CapturingDomainEventDispatchHandler` appends its name to a shared call list so tests can assert handler order. `CancellationCapturingDomainEventDispatchHandler` remembers the token of its last call. `ThrowingDomainEventDispatchHandler` always fails with an `InvalidOperationException`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Domain`. C# namespace: `PandaBear.Testing.Domain`. Assembly/project identity: `SharedKernel.Testing.Domain`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
