# SharedKernel.BuildingBlocks

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable value-object and workflow building blocks shared by bounded contexts.

## Identity and optional lookups

`IIdentified<TId>` exposes a stable identifier for any identified model. It is owned by this package;
domain entities compose it through `SharedKernel.Domain.IEntity<TId>`.

`Option<TEntity>.ToNotFoundResult(id, container)` maps an empty optional identified model to a
`SharedKernel.Results.Result<TEntity>` with a standard not-found detail. `container` is optional and
preserves aggregate context such as `"this tour"`. This package references `SharedKernel.Results` for
the result type.

## Compensation

`Compensation.CompleteOrCompensate` runs a follow-up operation and invokes a caller-provided
compensation action if that operation fails. Use it for small best-effort cleanup around side
effects that cannot share a transaction.

## Hypermedia links

`HypermediaLink` is an immutable record with `Rel`, `Href`, and `Method`. `HypermediaLink.Create` returns a
`Result<HypermediaLink>`; a null, empty, or whitespace value is an `Invalid` result that names the first
offending field (`rel`, `href`, or `method`). The JSON wire shape is
`{"rel":"self","href":"/items/1","method":"GET"}`, produced and read with System.Text.Json attributes only, so
it works with a consumer's source-generated `JsonSerializerContext` and needs no reflection.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.BuildingBlocks`. C# namespace: `PandaBear.BuildingBlocks`. Assembly/project identity: `SharedKernel.BuildingBlocks`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Payment identifiers

`Iban`, `Bic`, `Cpf`, `Cnpj`, and `PixKey` are immutable value objects in `PandaBear.BuildingBlocks`.
Create them from canonical input through `Result<T>` factories; invalid input never becomes an instance.
No additional package or runtime network connection is required.

```csharp
using PandaBear.BuildingBlocks;

var result = Iban.Create("DE89370400440532013000");
if (result.IsSuccess)
{
    Iban recipientIban = result.Value;
    // Pass recipientIban to the authorized account workflow.
}

var company = Cnpj.Create("12ABC34501DE35");
var pix = PixKey.Create(PixKeyType.Cnpj, "12ABC34501DE35");
```

Supply uppercase IBAN/BIC/CNPJ, eleven ASCII digits for CPF, and no display punctuation or spaces.
Pix email and EVP keys must be lowercase. Select `PixKeyType` explicitly; `None` and undefined enum
values are rejected. Leading zeros are preserved. Equality includes concrete type and canonical value;
Pix equality includes both type and value. Numeric and alphanumeric CNPJ are supported.

IBAN checks use all 89 layouts in SWIFT Registry release 103 (September 2026), valid check digits
and MOD97-10; national bank-specific checksum algorithms are outside this contract. BIC checks ISO
9362 eight/eleven-character syntax, not country assignment, registration or network reachability.
Pix checks BCB DICT API 2.12.1 formats, including alphanumeric CNPJ, and CPF/CNPJ check digits.
The CNPJ algorithm follows Receita Federal's alphanumeric specification. See the
[payment identifier plan and sources](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/plans/payment-identifiers.md).

Format validation never proves ownership, registration or account existence. Canonicalization,
recipient verification, masking, authorization, encryption and financial policy belong to consumers.
`Value` explicitly exposes sensitive plaintext: do not include it in ordinary APIs or telemetry.
`ToString()` and factory errors omit identifiers; structured logging/property serialization can still
expose `Value`. Do not destructure these objects in logs. JSON/EF adapters must call factories and
handle validation failure; this package supplies no deserialization or persistence bypass.
