# SharedKernel.Idempotency

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Storage-neutral idempotency primitives for request replay protection, message inbox processing, and projection
deduplication.

Persistence implementations belong in bounded-context infrastructure projects.

## Concepts

- `IdempotencyScope`: a stable namespace for keys, such as a handler, endpoint, inbox, or projection.
- `IdempotencyKey`: a caller- or message-provided opaque token that is unique inside a scope. Values must be 1
  to 255 characters and can contain only letters, digits, `.`, `_`, `:`, or `-`. Use UUIDv4, UUIDv7, ULID, or
  similarly high-entropy random values; do not include personal data or business identifiers.
- `IdempotencyOperation`: the pair of scope and key used by stores.
- `IIdempotencyStore`: storage-neutral contract for starting, completing, and reading idempotent operations.

Inbox/outbox tables and projection checkpoints can use these primitives without depending on HTTP, messaging, or a
specific database provider.

## Request fingerprints

`IdempotencyRequestFingerprint.Create("operation", "value")` returns uppercase SHA-256 hexadecimal text
for ordered string components. It hashes UTF-8 bytes of colon-separated frames; each frame contains the
invariant decimal UTF-16 string length, a colon, and the unchanged value. Length framing distinguishes
component boundaries even when values contain colons. Empty strings and an empty list are supported;
null lists or components are rejected. The primitive does not trim, normalize, or apply business validation.
Callers define stable component ordering and perform domain validation before fingerprinting.

Fingerprints allow replay checks without storing raw inputs. Hashes are not encryption and do not hide
low-entropy values from guessing. Do not use this API for password storage or authentication.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Idempotency`. C# namespace: `PandaBear.Idempotency`. Assembly/project identity: `SharedKernel.Idempotency`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
