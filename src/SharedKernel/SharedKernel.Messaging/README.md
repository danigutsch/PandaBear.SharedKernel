# SharedKernel.Messaging

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Storage-neutral messaging primitives for event envelopes and message metadata.

This package owns concepts that are not specific to domain events, typed integration events, event
sourcing, or one transport provider.

## Usage

Use `EventEnvelope` for storage-neutral metadata and encoded payloads. Implement `IEventEnvelopePublisher.Publish` at the transport boundary and propagate its cancellation token. Typed integration contracts and provider persistence belong in their respective integration-event modules.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Messaging`. C# namespace: `PandaBear.Messaging`. Assembly/project identity: `SharedKernel.Messaging`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
