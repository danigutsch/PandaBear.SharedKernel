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
it works with a consumer's source-generated `JsonSerializerContext` and needs no reflection. Deserializing a blank or missing value throws `ArgumentException`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.BuildingBlocks`. C# namespace: `PandaBear.BuildingBlocks`. Assembly/project identity: `SharedKernel.BuildingBlocks`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Contact, address and money values

`EmailAddress`, `PhoneNumber`, `PersonName`, `CountryCode`, `PostalAddress`, `Currency` and `Money` are immutable value
objects. Unlike the payment identifiers they sanitize free text first (trimmed, whitespace-normalized, control
characters removed). They check shape only: an email is not proof of an existing mailbox, a phone number is not proof of
service. `Currency` accepts only the active ISO 4217 alphabetic codes that denote money (see Currencies below), and
`CountryCode` accepts the officially assigned ISO 3166-1 alpha-2 codes only. `Money` rounds away from zero to the currency's minor unit and
never converts between currencies. Personal-data types redact `ToString()`. Validation messages never echo the input and
use camel-case field names such as `firstName` and `postalCode`.

```csharp
using PandaBear.BuildingBlocks;

var email = EmailAddress.Create("  Ana@Example.com ");           // ana@example.com
var phone = PhoneNumber.Create("+55 (11) 91234-5678");           // +5511912345678
var name = PersonName.Create("Ana", "Silva");
var price = Money.Create(1999.995m, Currency.Brl);               // BRL 2000.00
var address = PostalAddress.Create("Rua A, 10", null, null, "01000-000", "Sao Paulo", "SP", "BR");
```

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

## Country names

`CountryCode.EnglishName` comes from a static table embedded in the library, with one entry for each of the 249 assigned
codes. It does not use ICU/NLS data, `RegionInfo`, resource files or the current culture, so the name is the same on every
host, also with `InvariantGlobalization=true`. The names are the Unicode CLDR English territory names (for example `Brazil`,
`Türkiye`, `Côte d'Ivoire`, `Congo - Kinshasa`), cross-checked against the ISO 3166-1 English short names, with `&` written
as `and`, "St." spelled `Saint` and a plain ASCII apostrophe; accents are kept. The table is the source of truth: update it when ISO assigns a code.

## Currencies

`Currency.Create` accepts only the active ISO 4217 alphabetic codes that denote money, 164 codes taken from the SIX Group
ISO 4217 list one (withdrawn codes such as `HRK`, `BGN`, `ANG` or `ZWL` are rejected), including the funds codes that have
a minor unit (`CLF`, `UYI`, `UYW`, `BOV`, `CHE`, `CHW`, `COU`, `MXV`, `USN`). Precious metals (`XAU XAG XPT XPD`), the test
code `XTS`, the no-currency code `XXX`, the SDR `XDR`, the bond-market and fund units (`XBA XBB XBC XBD XSU XUA`) and the
Arab Accounting Dinar `XAD` are not money and are rejected. An unknown code is an input-independent validation error. The
same table gives `MinorUnits`: 0 for `BIF CLP DJF GNF ISK JPY KMF KRW PYG RWF UGX UYI VND VUV XAF XOF XPF`, 3 for
`BHD IQD JOD KWD LYD OMR TND`, 4 for `CLF UYW` and 2 for every other currency.

## Dates, codes and exact money

- `DateOnlyRange.Create(start, end)`: a range of calendar days with both ends included (`Days`); a missing date (`default`) is reported as required on `startDate` or `endDate`, and an end before the start on `endDate`.
- `BirthDate.Create(value, today, minAgeYears, maxAgeYears)`: required, not in the future, within the age limits; `AgeOn(day)` gives whole years. Leap-day births follow the 1 March convention: in a non-leap year the birthday is reached on 1 March (not 28 February), which is the stricter reading for minimum-age checks; in leap years it is 29 February.
- `ReferenceCode.Create(value, maxLength, field, label)`: ASCII letters, digits, `-` and `_`, trimmed and upper-cased, so codes that differ only in case are equal.
- `Money.CreateExact(amount, currency, field, label)`: like `Money.Create` but rejects an amount with more decimals than the currency's minor unit instead of rounding it.

## Business time

`BusinessTimeProvider(businessTimeZone, inner = null)` is a `TimeProvider` that reports the configured business zone (for example `America/Sao_Paulo`) as its `LocalTimeZone`; UTC time and timers come from the inner provider (default `TimeProvider.System`). `GetLocalNow()` therefore follows the business day rather than the server's zone, and `provider.Today()` (`TimeProviderExtensions`) returns that local calendar day as a `DateOnly`, for example for `BirthDate.Create`. Register it as the app's `TimeProvider`.

## Concurrency conflicts

`ConcurrencyConflictException` signals that a save found the row changed or gone since it was read. Like `UniqueConstraintViolationException` it is persistence-neutral: the provider exception is kept as the inner exception and the message never contains data values. `SharedKernel.EntityFrameworkCore` raises it from `SaveChangesTranslatingConflicts`.
