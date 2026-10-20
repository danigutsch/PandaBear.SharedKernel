# SharedKernel.Messaging.IntegrationEvents

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Typed integration event dispatch abstractions for events crossing bounded-context boundaries.

The core contracts do not reference CloudEvents or transport SDK types.

## Usage

Define typed `IIntegrationEvent` contracts and `IIntegrationEventHandler<TIntegrationEvent>` implementations. Register contracts/consumers with explicit event type names and `JsonTypeInfo<T>` using the service collection extensions. Enqueue through the outbox interfaces; provider persistence and host composition remain separate packages.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Messaging.IntegrationEvents`. C# namespace: `PandaBear.Messaging.IntegrationEvents`. Assembly/project identity: `SharedKernel.Messaging.IntegrationEvents`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
