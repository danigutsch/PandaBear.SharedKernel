# SharedKernel.Idempotency.AspNetCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

ASP.NET Core adapters for the `PandaBear.SharedKernel.Idempotency.*` family: the `Idempotency-Key` request header and a
data protector bound to an opaque storage-entry key.

## What is inside

- `IdempotencyKeyHeader.Create(value)`: creates a required `IdempotencyKey` from HTTP input as a `Result<IdempotencyKey>`.
  The value object owns token rules and normalization; this adapter associates missing or invalid input with the standard
  `Idempotency-Key` validation field. It does not generate keys, coordinate operations, or log header values.
- `KeyBoundDataProtector`: protects and unprotects values with a purpose derived from a storage-entry key, so a protected
  payload only opens for the entry it was written for.

## Install

```sh
dotnet add package PandaBear.SharedKernel.Idempotency.AspNetCore
```

```csharp
using PandaBear.Idempotency.AspNetCore;

var key = IdempotencyKeyHeader.Create(httpContext.Request.Headers[IdempotencyKeyHeader.Name]);
var protector = new KeyBoundDataProtector(dataProtectionProvider, "orders-idempotency");
var protectedBytes = protector.Protect("entry-key", payload);
```

## Migrating from SharedKernel.AspNetCore

`IdempotencyKeyHeader` and `KeyBoundDataProtector` moved from `PandaBear.SharedKernel.AspNetCore` (namespace
`PandaBear.AspNetCore`) into this package and namespace `PandaBear.Idempotency.AspNetCore`. Add the package reference
and replace `using PandaBear.AspNetCore;` with `using PandaBear.Idempotency.AspNetCore;` where they are used.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Idempotency.AspNetCore`. C# namespace: `PandaBear.Idempotency.AspNetCore`.
Assembly/project identity: `SharedKernel.Idempotency.AspNetCore`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured
local/private feed. This README does not imply availability on nuget.org.
