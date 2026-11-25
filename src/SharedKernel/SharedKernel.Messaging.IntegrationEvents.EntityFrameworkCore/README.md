# PandaBear.SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Entity Framework Core outbox and inbox persistence provider for typed integration-event messaging.

## Usage

Compose the outbox/inbox registration extensions with the owning EF Core context and configure `IntegrationEventStorageOptions` and relay options. The package includes durable outbox storage, claiming, relay, and PostgreSQL transport components. Producers and consumers register separately. Applications retain schemas, migrations, transport composition, handler business rules, and retention policy.

Set `StorePublishExceptionTypeOnly = true` on an outbox relay to store only the publisher exception type in
`LastPublishError`. The option applies to future failures for the configured context. The default stores bounded
exception text. Applications govern access to the field and retention of existing records.

Transport consumers retry a failing message without limit unless `IntegrationEventOutboxRelayOptions.MaxConsumeAttempts` is set. When a message reaches that limit it is poisoned: it stops being claimed, so later messages for the same consumer keep flowing. Use `IntegrationEventTransportPoison.CountPoisoned` to alert on poisoned messages and `IntegrationEventTransportPoison.ReleasePoisoned` to return one to delivery with a fresh attempt budget after the cause is fixed. Release from an audited operator workflow; the package does not record who released a message. Adding the poison column and index requires a migration in the context that owns the transport table. `LastConsumeError` holds only the exception type name, never its message, because messages can embed payload or personal data. Each failed attempt is logged through `ILogger` with the exception, consumer name, message id and attempt number; poisoning logs at error level. Read the exception text from logs, not the table.

`IntegrationEventMessagingHealth.ReadOutbox` reports unpublished messages and publication retries.
`ReadTransport` reports terminal failures and delivery retries for one selected consumer. Each
method reads counts and oldest timestamps together from the supplied context's table mappings.
Retries with timestamps at or before the specified cutoff are due, including ones with an active
lease. The results contain counts and timestamps. Applications derive ages and evaluate their own
threshold policies.

A separate query context can map the writer's outbox table with
`AddIntegrationEventOutboxStorage<TContext>()`. Set its schema and table name to match the
writer through `ConfigureIntegrationEventStorage<TContext>()` and set
`ExcludeOutboxFromMigrations` on the query context. The mapping leaves writer and relay
registration with the owning context.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

public static class Example
{
    public static IServiceCollection RegisterProducer(IServiceCollection services)
        => services.AddIntegrationEventOutbox<AppDbContext>();
}

public sealed class AppDbContext : DbContext;
```

## Delivery inspection

Use `IntegrationEventDeliveryInspection.ReadOutbox` with the outbox storage mapping and
`ReadTransport` with the transport storage mapping and an exact consumer name. The queries
return pending rows and include inbound terminal failures. You can filter by exact event ID,
event type, or an inclusive UTC storage-time cutoff. Pages hold up to 100 rows and are ordered
by timestamp and storage identifier. Pass the returned cursor with the same scope and filters
to continue. The storage can change between queries as normal delivery completes.

Use `ReadMatchingOutbox` or `ReadMatchingTransport` to restrict a page to event identifiers
from a durable application relationship. Supply a query from the same EF Core context that
selects the exact stored envelope identifier strings. The provider applies that query and
the delivery filters in one statement before paging. Keep the same identifier query when
continuing a page. The reader's database principal needs permission to read the selected
application metadata as well as the delivery metadata.

Use `IntegrationEventDeliveryInspection.ReadRetainedOutbox` with the same outbox mapping for
an exact event ID lookup, including after successful publication. The result includes the
publication time, failed attempt count and retry timestamps. An absent or purged row returns
no result. The lookup uses the existing unique event ID index and requires no storage migration.

Pages contain delivery metadata only. Keep raw payloads, extension attributes and error details
outside the operator projection. The application controls access to event identifiers and any
workflow that claims, replays, exports or removes a message.

## Completed inbox duplicates

For an owned PostgreSQL inbox, add
`AddPostgreSqlIntegrationEventInboxDuplicates<TContext>(consumerName, inboxScopePrefix)`
alongside the existing inbox configuration. Apply the context's migration before deploying a
caller. The opt-in model adds a separate observation table in the inbox schema. Pass a distinct
table name when multiple owners share a schema. The foreign key to the inbox entry applies the
existing retention decision to its observation, including any retained ownership hold. Existing
entries start with no observations and need no backfill.

Resolve the scoped `IntegrationEventInboxDuplicateRecorder<TContext>` and call `Record` when
handling was skipped because the inbox operation is complete. Supply the exact inbox scope,
typed event ID and observation time. The operation key uses the event GUID in N format. The
recorder matches the scope to the registered consumer using an ordinal prefix comparison.
Keep bindings stable across deployments. Bindings for a context use one table and cannot
overlap. The write confirms that the entry is completed, increments the counter atomically up
to the maximum signed 32-bit value and keeps the latest observation time. An unregistered
scope, a missing or in-progress entry, or a different operation for an already observed
consumer and event produces no observation. The call participates in the current transaction.
Only an explicit call to `Record` writes a duplicate observation.

Use `ReadPage` and `ReadHealth` on an opted-in context with the exact consumer name. A page
contains event IDs, counts and latest observation times, with an optional exact event filter
and a maximum of 100 rows. The `ReadPage` overload with `observedSince` includes only rows
whose latest observation falls at or after that instant. Pass the same consumer and filters
when continuing. Observation times can advance between reads. Health returns counts for all
retained observations. Application authorization, migration, and retention policy apply to
both read and write use.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore`. C# namespace: `PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore`. Assembly/project identity: `SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
