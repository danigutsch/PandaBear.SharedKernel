# SharedKernel.Messaging

Storage-neutral messaging primitives for event envelopes and message metadata.

This package owns concepts that are not specific to domain events, typed integration events, event
sourcing, or one transport provider.

## Usage

Use `EventEnvelope` for storage-neutral metadata and encoded payloads. Implement `IEventEnvelopePublisher.Publish` at the transport boundary and propagate its cancellation token. Typed integration contracts and provider persistence belong in their respective integration-event modules.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Messaging`. C# namespace: `PandaBear.Messaging`. Assembly/project identity: `SharedKernel.Messaging`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
