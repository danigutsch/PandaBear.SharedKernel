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

## Compose EF integration-event storage and delivery

The existing `PandaBear.SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore` package offers
`AddIntegrationEvents<TContext>()` for context-qualified composition. It uses the same registrations
as the individual `AddIntegrationEventOutbox`, `AddIntegrationEventInbox`, and PostgreSQL transport
extensions. No additional package or transport dependency is needed.

A producer host can own an outbox and relay messages to a durable PostgreSQL destination:

```csharp
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

services.AddIntegrationEvents<ProducerDbContext>()
    .WithOutbox(storage =>
    {
        storage.OutboxSchema = "producer";
        storage.TransportSchema = "messaging";
        storage.ExcludeTransportFromMigrations = true;
    })
    .WithOutboxRelay(options => options.BatchSize = 20)
    .WithPostgreSqlAtomicClaims()
    .WithPostgreSqlTransportProducer("destination");
```

The outbox and business data must commit in the same owning context transaction. When another
context owns transport migrations, exclude that table from producer migrations as above; one owner
must create it. Omit the exclusion when this context owns the transport table. Unconfigured storage
keeps the existing `messaging` schema and table names.

A dedicated consumer worker explicitly enables background delivery:

```csharp
services.ConfigureIntegrationEventStorage<ConsumerDbContext>(storage =>
{
    storage.TransportSchema = "messaging";
    storage.ExcludeTransportFromMigrations = true;
});
services.AddIntegrationEvents<ConsumerDbContext>()
    .WithInbox(storage => storage.Schema = "consumer")
    .WithPostgreSqlTransportConsumer("destination", options => options.BatchSize = 20)
    .WithHostedConsumer();
```

`WithPostgreSqlTransportConsumer` registers the consumer without starting it. Only the process that
owns delivery should call `WithHostedConsumer`; an API that delegates delivery to a standalone worker
must omit it. Hosting requires a consumer for the same context and is idempotent. Fluent composition
rejects a second consumer or producer registration for the same context instead of silently replacing
its queue or destination.

Register each co-hosted module using its own context. Outboxes, inbox stores, transport producers,
storage options, and relay options remain context-qualified. Resolve outbox and inbox interfaces with
`GetRequiredKeyedService<T>(typeof(TContext))`. The unkeyed `IIntegrationEventOutbox` and `IIdempotencyStore`
resolve to the only registered context, or to the context chosen with
`UseDefaultIntegrationEventOutbox<TContext>()` / `UseDefaultIdempotencyStore<TContext>()` (fluent:
`AsDefaultOutbox()` / `AsDefaultInbox()`). When several contexts register one and none is chosen, resolving
the unkeyed service throws instead of silently binding whichever registered first. Register an
`IIntegrationEventSerializer` globally or keyed by context.

Transport producers register only context-keyed `IEventEnvelopePublisher` instances. Relays prefer their
context-keyed publisher and otherwise require an unkeyed application publisher. Transport consumers
always require the unkeyed application dispatcher; they must never dispatch back through a producer.
Neither path borrows another context's keyed publisher. Configure application event handlers and
business destinations in the consuming application.

Existing individual registration methods retain their behavior, including automatic hosting by
`AddPostgreSqlIntegrationEventTransportConsumer`. Use either registration style for each component;
the fluent API does not require migrating existing single-context callers.
