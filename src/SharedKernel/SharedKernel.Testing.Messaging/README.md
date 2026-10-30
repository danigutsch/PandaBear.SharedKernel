# SharedKernel.Testing.Messaging

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Event envelope publisher and integration event handler fakes for SharedKernel test suites.

## Usage

```csharp
using PandaBear.Testing.Messaging;

var publisher = new RecordingEventEnvelopePublisher();
await relay.Run(publisher, ct);
publisher.Published.ShouldHaveSingleItem();
```

`RecordingEventEnvelopePublisher` stores published envelopes and can fail every call or only the first. `StaticRecordingEventEnvelopePublisher` records into a static list for publishers the container creates by type. `DisposableEventEnvelopePublisher` and `AsyncDisposableEventEnvelopePublisher` count disposals. `ControlledEventEnvelopePublisher` blocks its first two calls until released. `DelegatingEventEnvelopePublisher` forwards to a wrapped publisher. `CapturingIntegrationEventHandler<TIntegrationEvent>` records the events it handles in a static list per event type.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Messaging`. C# namespace: `PandaBear.Testing.Messaging`. Assembly/project identity: `SharedKernel.Testing.Messaging`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
