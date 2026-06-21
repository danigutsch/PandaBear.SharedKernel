# SharedKernel.Idempotency

Storage-neutral idempotency primitives for request replay protection, message inbox processing, and projection
deduplication.

This project intentionally contains abstractions only. Persistence implementations belong in bounded-context
infrastructure projects.

## Concepts

- `IdempotencyScope`: a stable namespace for keys, such as a handler, endpoint, inbox, or projection.
- `IdempotencyKey`: a caller- or message-provided opaque token that is unique inside a scope. Values must be 1
  to 255 characters and can contain only letters, digits, `.`, `_`, `:`, or `-`. Use UUIDv4, UUIDv7, ULID, or
  similarly high-entropy random values; do not include personal data or business identifiers.
- `IdempotencyOperation`: the pair of scope and key used by stores.
- `IIdempotencyStore`: storage-neutral contract for starting, completing, and reading idempotent operations.

Inbox/outbox tables and projection checkpoints can use these primitives without depending on HTTP, messaging, or a
specific database provider.
