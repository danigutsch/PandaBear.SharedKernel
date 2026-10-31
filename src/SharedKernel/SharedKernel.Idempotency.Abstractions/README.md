# PandaBear.SharedKernel.Idempotency.Abstractions

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Storage-neutral idempotency primitives for request replay protection, message inbox processing, and projection
deduplication.

The separate EntityFrameworkCore package supplies reusable persistence; applications own its configuration and transactions.

## Concepts

- `IdempotencyScope`: a stable namespace for keys, such as a handler, endpoint, inbox, or projection.
- `IdempotencyKey`: a caller- or message-provided opaque token that is unique inside a scope. Values must be 1
  to 255 characters and can contain only letters, digits, `.`, `_`, `:`, or `-`. Use UUIDv4, UUIDv7, ULID, or
  similarly high-entropy random values; do not include personal data or business identifiers.
- `IdempotencyOperation`: the pair of scope and key used by stores.
- `IIdempotencyStore`: storage-neutral contract for starting, completing, and reading idempotent operations. The
  `TryStart` overload that takes a `retentionOwner` records an opaque owner key (ASCII letters, digits, `.`, `_`, `:`,
  `-`; at most 128 characters) on the entry and `IdempotencyEntry.RetentionOwner` returns it. A store that predates
  the overload keeps working: the default implementation accepts `null` and throws `NotSupportedException` for an owner.
- `IIdempotencyRetentionStore`: optional contract for purging one bounded batch of expired entries. The caller passes a
  cutoff, an optional owner-tracked scope prefix and the held owner keys, and gets purged, held and unattributed
  counts. An expired entry whose owner is held is never deleted. Inside an owner-tracked scope an expired entry
  without an owner is retained and counted as unattributed (fail-closed); outside it entries purge as before.

Inbox/outbox tables and projection checkpoints can use these primitives without depending on HTTP, messaging, or a
specific database provider.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Idempotency.Abstractions`. C# namespace: `PandaBear.Idempotency`. Assembly/project identity: `SharedKernel.Idempotency.Abstractions`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Ownership boundary

This package defines validated operation identity and the persistence contract: keys, scopes,
operations, entries, lifecycle state, and start results. Validation enforces those value contracts.
Retention periods, which scopes are owner-tracked, the held owner keys and purge evidence stay in the host.
It does not execute commands, fingerprint payloads, schedule retries, or choose transaction policy.

`PandaBear.SharedKernel.Idempotency.EntityFrameworkCore` implements persistence separately.
`PandaBear.SharedKernel.HttpClients` owns explicit client retry state. Applications own request
fingerprinting, authorization binding, transaction boundaries, and replay interpretation.

## Preview migration

The package ID replaces `PandaBear.SharedKernel.Idempotency`. Update direct package references and
lock files together. The `PandaBear.Idempotency` namespace remains unchanged. The project and assembly are now
`SharedKernel.Idempotency.Abstractions`. Request fingerprint helpers have moved to the consuming application; preserve
its existing framing when migrating persisted records. Do not install both package identities.
