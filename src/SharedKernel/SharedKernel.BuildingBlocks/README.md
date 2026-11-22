# SharedKernel.BuildingBlocks

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable value-object and workflow building blocks shared by bounded contexts.

## Source layout

Source files are grouped by topic. The folders are organization only: every public type keeps the single
`PandaBear.BuildingBlocks` namespace, so moving between folders never changes the API.

| Folder | Contents |
| --- | --- |
| `Foundation` | `ValueObject`, `IIdentified`, `ReferenceCode`, `Slug`, `WebUrl`, text rules, option-to-result helpers |
| `Localization` | `ContentCulture`, `SupportedCultures`, `CultureLanguageTags`, `TranslationSet` |
| `Contact` | `EmailAddress`, `PhoneNumber`, `PersonName`, `PostalAddress` |
| `Geography` | `CountryCode`, `CountryNames` |
| `Finance` | `Money`, `Currency`, `CurrencyCodes`, `Iban`, `Bic` |
| `Brazil` | `Cpf`, `Cnpj`, `PixKey`, `PixKeyType`, check-digit helpers |
| `Time` | `BirthDate`, `DateRange`, `DateOnlyRange`, `BusinessTimeProvider`, `TimeProvider` helpers |
| `Failures` | concurrency and unique-constraint exceptions, cancellation helpers, `Compensation` |
| `Http` | `HypermediaLink` |
| `Paging` | `PageWindow` |

The test project mirrors these folders; shared test helpers live next to the types they exercise, and
`RestoreTests` (which spans several types) stays at the root.

## Slugs and content cultures

`Slug.Create` turns free text into a canonical URL slug (lowercase ASCII letters and digits joined by single hyphens,
diacritics stripped, default maximum `Slug.DefaultMaxLength` = 128). Control characters, surrogates (including lone
ones), `/ \ ? #`, `.`, and any other non-ASCII character are rejected rather than transliterated, and the error never
echoes the input. A slug therefore never contains `.`, which keeps it apart from file names in paths.
`Slug.IsCanonical` checks an already-stored slug.

`Slug.CreateLenient` never rejects a character: it strips diacritics like `Create`, lowercases, turns every run of other characters
(punctuation, `/ \ ? # .`, control characters, surrogates, letters without an ASCII base such as `ß` or `東`) into one hyphen, trims
the hyphens at both ends and cuts a slug longer than `maxLength` without leaving a trailing hyphen. Use it to derive a name from text a
person wrote, such as a download file name from a trip code (`ÁLPES/2027 ..\x` gives `alpes-2027-x`). It fails only when the text is
blank (`{label} is required.`) or keeps no ASCII letter or digit (`中国` gives `{label} must contain a letter or a digit.`), so the caller
can fall back to a fixed name. Unicode normalization comes from .NET; there is no transliteration table.

## Page windows

`PageWindow.Create(requestedPage, pageSize, total)` computes the page a request shows from the page number in the URL, as Microsoft's
Blazor guidance recommends for the current page of a paged list
([State management using the URL](https://learn.microsoft.com/aspnet/core/blazor/state-management/?view=aspnetcore-11.0#state-management-using-the-url)).
The requested page is clamped like `QuickGrid`'s URL paging: missing, malformed, zero or negative shows the first page, beyond the
end the last page, and an empty list has one page. The `string` overload takes the raw query value (only ASCII digits are a number;
digits beyond `int` mean beyond the end); the `int?` overload takes a value already parsed. The window gives `Page` (1-based),
`PageCount`, `Skip`, `Take` (the page size), `PreviousPage` and `NextPage` (`null` at either end) and `HasMultiplePages`.

`IsOutOfRange` is `true` when the request named a page that does not exist (below 1, beyond the end, or text that is not a page
number) and `false` when it named none. A public site answers such a request with 404 Not Found, so crawlers do not index endless
copies of the last page; an admin page may show the clamped page instead.

This is offset paging, which numbered pages need ([GitLab pagination guidelines](https://docs.gitlab.com/development/database/pagination_guidelines/)
compare it with keyset paging). Order the query by a unique, immutable key, for example the sort column and then the id, or rows
move between pages from one request to the next. Count first, then read the page:

```csharp
var total = await photos.CountAsync(cancellationToken);
var window = PageWindow.Create(requestedPage, pageSize: 24, total);
if (window.IsOutOfRange)
{
    return TypedResults.NotFound();
}

var page = await photos.OrderBy(photo => photo.SortOrder).ThenBy(photo => photo.Id)
    .Skip(window.Skip).Take(window.Take).ToListAsync(cancellationToken);
```

`Pager` in `PandaBear.SharedKernel.Blazor.Site` renders the links for a window.

## Web addresses

`WebUrl.Create` accepts an absolute `http` or `https` address that is safe to render as a link, such as a sign-up form or a
social profile. It must start with `http://` or `https://` in any case, name a host, and carry no user information (not
even an empty one, `https://@host`). Relative and protocol-relative addresses (`//host`), `https:host`, every other scheme
(`javascript:`, `data:`, `mailto:`), white space or control characters anywhere (rejected, not trimmed, because browsers strip
some of them and that can hide a scheme), backslashes, lone surrogates, text not in Unicode normalization form C, and
addresses longer than `WebUrl.MaxLength` (2048) are rejected; the error names the `field` and `label` and never echoes the
input. `Value` keeps the path, query and fragment as supplied and lower-cases the scheme and host, so
`HTTPS://Example.ORG/Tours` becomes `https://example.org/Tours` and addresses that differ only there are equal. The checks
reuse `PandaBear.InputNormalization.WebAssetUriSanitizer`, with an `http` address checked through its `https` form.
`WebUrl.CreateHttps` applies the same rules but accepts `https` only: an `http://` address is rejected like any other scheme
(`{label} must be an absolute https address.`), for a link that must not downgrade, such as a video on another site.
`WebUrl.Restore` rebuilds a stored value without validation.

`ContentCulture` is a path-safe language tag in canonical casing (`pt`, `pt-BR`, `zh-Hant-TW`); cultures that differ only in
case are equal, and `Parent` walks `pt-BR` to `pt`. Casing follows BCP 47: script title case and region upper case only before
the first singleton, so `en-u-ca-gregory` and `de-DE-x-ab` stay lower case after the `u` or `x`. It is a path-safe
culture-name check (hyphen-separated ASCII letters and digits, a two-to-eight-letter first part, later subtags of one to eight
characters, a singleton followed by a subtag, at most 35 characters), not full BCP 47 validation: subtag order and registry entries
are not checked; `Parent` drops a singleton left without a subtag (`en-x-ab` gives `en`). `SupportedCultures` is an immutable set of allowed cultures with a default:
`Resolve` maps a requested culture to an allowed one (`pt-BR` to `pt`, or `pt` to `pt-BR`) and `TryGet` matches exact
text such as a URL segment. `TranslationSet.Create` holds the translations of one entity, and `TryResolve` resolves in the same
order as `SupportedCultures.Resolve`: the requested culture, its neutral parents, the first translation of the same language in
the order given (`pt` to `pt-BR`), then the default culture and its parents, or reports none.

`CultureLanguageTags` names the language each supported culture's content is written in, for a site whose culture (the content key
and the address segment, `pt`) is not that language (`pt-BR`). Pages declare the tag (`lang`, `hreflang`, `Content-Language`,
`og:locale`) and format with it, while addresses and stored keys keep the culture. `Create` takes only the cultures that declare
another tag (`new CultureLanguageTag(pt, ptBR)`); every other culture declares itself. It is invalid, with a message that names the
culture or tag, when a culture is not supported, has two tags, or when two cultures would declare the same tag (a mapping to `en`
while `en` declares itself), so a failed set stops the application at start-up through `.Value`. `LanguageTagOf` maps a culture to
its tag, `LanguageTags` and `DefaultLanguageTag` list them in culture order for request localization, and `TryGetCulture` maps a tag
(exact, ignoring case, such as `CultureInfo.CurrentUICulture.Name`) back to its culture for building addresses. The ASP.NET Core
`PathCultureRequestCultureProvider` in `PandaBear.SharedKernel.AspNetCore.Localization` takes it to make the tag the request culture.

```csharp
var pt = ContentCulture.Restore("pt");
var en = ContentCulture.Restore("en");
var supported = SupportedCultures.Create(pt, [pt, en]).Value;
var languageTags = CultureLanguageTags.Create(supported, [new CultureLanguageTag(pt, ContentCulture.Restore("pt-BR"))]).Value;

languageTags.LanguageTagOf(pt);                     // pt-BR
languageTags.LanguageTagOf(en);                     // en
languageTags.TryGetCulture("pt-BR", out var key);   // true, key = pt
```

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

The messages start with an English noun (`Phone number is required.`, `Birth date cannot be in the future.`, `Street is required.`). To
make them match a form's labels, pass `field` and `label` to `PhoneNumber.Create(value, field, label)` and
`BirthDate.Create(value, today, minAgeYears, maxAgeYears, field, label)` (the age messages start with `Age`), and a
`PostalAddressLabels` to `PostalAddress.Create`, setting only the parts that differ; the address keeps its field keys:

```csharp
var whatsapp = PhoneNumber.Create(input, field: "whatsapp", label: "WhatsApp");   // "WhatsApp is required."
var home = PostalAddress.Create(street, null, null, cep, city, state, "BR", new PostalAddressLabels { PostalCode = "CEP" });
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

Supply uppercase IBAN/BIC/CNPJ, eleven ASCII digits for CPF, and no display punctuation or spaces. A rejected value gives one
input-independent message that can be shown to people: `IBAN is not valid.`, `BIC is not valid.`, `CPF is not valid.`,
`CNPJ is not valid.` or `Pix key is not valid.`, on the field `iban`, `bic`, `cpf`, `cnpj` or `pixKey`.

For a CPF typed into a form, `Cpf.CreateFromInput` lets the person group the digits as they are used to: white space, dots and dashes
are ignored wherever they appear, so `529.982.247-25`, `529 982 247 25`, `529982247-25` and `52998224725` all give the CPF
`52998224725` (GOV.UK: [ignore unwanted characters before validating](https://design-system.service.gov.uk/patterns/national-insurance-numbers/),
[allow additional spaces, hyphens and dashes](https://design-system.service.gov.uk/patterns/bank-details/)). Any other character is
rejected. Text with nothing but separators gives `CPF is required.` and anything else that fails `CPF is not valid.`.
Pix email and EVP keys must be lowercase. Select `PixKeyType` explicitly; `None` and undefined enum
values are rejected. Leading zeros are preserved. Equality includes concrete type and canonical value;
Pix equality includes both type and value. Numeric and alphanumeric CNPJ are supported.

IBAN checks use all 89 layouts in SWIFT Registry release 103 (September 2026), valid check digits
and MOD97-10; national bank-specific checksum algorithms are outside this contract. BIC checks ISO
9362 eight/eleven-character syntax, not country assignment, registration or network reachability.
Pix checks [BCB DICT API](https://www.bcb.gov.br/content/estabilidadefinanceira/pix/API-DICT.html) 2.12.1 formats,
including alphanumeric CNPJ, and CPF/CNPJ check digits. The CNPJ algorithm follows Receita Federal's
[alphanumeric check-digit specification](https://www.gov.br/receitafederal/pt-br/centrais-de-conteudo/publicacoes/documentos-tecnicos/cnpj/manual-dv-cnpj.pdf).

Format validation never proves ownership, registration or account existence. Canonicalization,
recipient verification, masking, authorization, encryption and financial policy belong to consumers.
`Value` explicitly exposes sensitive plaintext: do not include it in ordinary APIs or telemetry.
`ToString()` and factory errors omit identifiers; structured logging/property serialization can still
expose `Value`. Do not destructure these objects in logs. JSON/EF adapters must call factories and
handle validation failure. Persistence mappers (EF value converters) that rebuild a value from stored text must use the
`Restore` factories instead of `Create(...).Value`: `EmailAddress.Restore`, `PhoneNumber.Restore`, `CountryCode.Restore`,
`Currency.Restore`, `PersonName.Restore(firstName, lastName)`, `Money.Restore(amount, currency)`, `Cpf.Restore(value)`,
`Iban.Restore(value)`, `Bic.Restore(value)`,
`BirthDate.Restore(value)` and `PostalAddress.Restore(street, complement, neighborhood, postalCode, city, state, country)`.
`BirthDate.Restore` skips the age limits, which depend on the day, and refuses only the default date
(`ArgumentOutOfRangeException`). `Restore` is for persistence mappers
only: it rebuilds a value from text this type produced earlier, performs no validation beyond null/blank checks (`ArgumentException`) and
the same trim and case normalization as `Create`, so a stored value that a newer rule rejects (an allow-list that tightened, a
longer-than-now name) still loads instead of throwing on every query. `CountryCode.Restore` keeps a code outside the assigned set (its
`EnglishName` is the code), `Currency.Restore` keeps a delisted code (2 minor units) and `Money.Restore` does not round. `Cpf.Restore`,
`Iban.Restore` and `Bic.Restore` trim and keep a value whose check digits or layout fail; `PostalAddress.Restore` sanitizes each part like `Create`, turns blank optional parts into
`null`, requires street, postal code and city, takes an already restored `CountryCode`, and does not enforce the part length limits. Never
use `Restore` for user input.

`Cpf.Formatted` returns the sensitive display mask `000.000.000-00` (for example on a printed contract); it is personal data like `Value`,
while `ToString()` stays redacted. A restored value that is not eleven digits is returned unchanged.

`Iban.Formatted` returns the sensitive ISO 13616 print format, groups of four separated by a space (`DE89 3704 0044 0532 0130 00`), for a
page or a printed contract; `ToString()` stays redacted. A restored value that is not only ASCII letters and digits is returned unchanged.

## Country names

`CountryCode.EnglishName` comes from a static table embedded in the library, with one entry for each of the 249 assigned
codes. It does not use ICU/NLS data, `RegionInfo`, resource files or the current culture, so the name is the same on every
host, also with `InvariantGlobalization=true`. The names are the Unicode CLDR English territory names (for example `Brazil`,
`Türkiye`, `Côte d'Ivoire`, `Congo - Kinshasa`), cross-checked against the ISO 3166-1 English short names, with `&` written
as `and`, "St." spelled `Saint` and a plain ASCII apostrophe; accents are kept. The table is the source of truth: update it when ISO assigns a code.

`CountryCode.AllByEnglishName` is the list for dropdowns: every code ordered by English name, ignoring case and diacritics (`Åland Islands`
sorts among the A names, `Türkiye` among the T names, `Zimbabwe` last). It does not use ICU or the current culture (a culture sort
becomes ordinal under `InvariantGlobalization=true` and would put accented names after `Zimbabwe`): the key folds the accented letters of the
table to ASCII (`Å ã`->`A`, `ç`->`C`, `é`->`E`, `í`->`I`, `ô`->`O`, `ü`->`U`), upper-cases ASCII letters, compares ordinally and breaks ties by
code. A test fails if a table name contains a character the key does not handle.

## Currencies

`Currency.Create` accepts only the active ISO 4217 alphabetic codes that denote money, 164 codes taken from the SIX Group
ISO 4217 list one (withdrawn codes such as `HRK`, `BGN`, `ANG` or `ZWL` are rejected), including the funds codes that have
a minor unit (`CLF`, `UYI`, `UYW`, `BOV`, `CHE`, `CHW`, `COU`, `MXV`, `USN`). Precious metals (`XAU XAG XPT XPD`), the test
code `XTS`, the no-currency code `XXX`, the SDR `XDR`, the bond-market and fund units (`XBA XBB XBC XBD XSU XUA`) and the
Arab Accounting Dinar `XAD` are not money and are rejected. An unknown code is an input-independent validation error. The
same table gives `MinorUnits`: 0 for `BIF CLP DJF GNF ISK JPY KMF KRW PYG RWF UGX UYI VND VUV XAF XOF XPF`, 3 for
`BHD IQD JOD KWD LYD OMR TND`, 4 for `CLF UYW` and 2 for every other currency.

### Displaying money

`Money.ToString()` stays the invariant `EUR 1.00` form for logs and storage. For people, `money.ToDisplayString(culture)` (or the `C`
format, as in `$"{price:C}"` or `string.Format(culture, "{0:C}", price)`) uses the culture's currency pattern, separators and negative
form, `Currency.Symbol` as the symbol and the currency's minor unit as the decimals:

| Culture | BRL | EUR | USD |
| --- | --- | --- | --- |
| pt-BR | `R$ 1.234,56` | `€ 1.234,56` | `$ 1.234,56` |
| pt-PT | `1 234,56 R$` | `1 234,56 €` | `1 234,56 $` |
| en-US | `R$1,234.56` | `€1,234.56` | `$1,234.56` |
| invariant | `R$1,234.56` | `€1,234.56` | `$1,234.56` |

`Currency.Symbol` comes from a fixed table (`R$`, `€`, `$`, `£`, `¥` for BRL, EUR, USD, GBP, JPY) and is the code itself for every other
currency (`CHF 1.234,56`), so the symbol never depends on the platform. `RegionInfo` is not used: one code has many regional symbols
(`US$`, `$`, `USD`), and a culture's own `CurrencySymbol` belongs to its region's currency, not to the money being shown. The pattern
and separators do come from the culture data, which .NET reads from ICU on Linux and macOS: a host that sets `InvariantGlobalization`
formats every culture like the invariant culture, and a container image without ICU must install it for pt-BR or pt-PT output.

## Dates, codes and exact money

- `DateOnlyRange.Create(start, end)`: a range of calendar days with both ends included (`Days`); a missing date (`default`) is reported as required on `startDate` or `endDate`, and an end before the start on `endDate`.
- `BirthDate.Create(value, today, minAgeYears, maxAgeYears)`: required, not in the future, within the age limits; `AgeOn(day)` gives whole years. Leap-day births follow the 1 March convention: in a non-leap year the birthday is reached on 1 March (not 28 February), which is the stricter reading for minimum-age checks; in leap years it is 29 February.
- `ReferenceCode.Create(value, maxLength, field, label)`: ASCII letters, digits, `-` and `_`, trimmed and upper-cased, so codes that differ only in case are equal.
- `Money.CreateExact(amount, currency, field, label)`: like `Money.Create` but rejects an amount with more decimals than the currency's minor unit instead of rounding it.
- `Money.CreateFromInput(amount, currency, field, label)`: for an amount a person typed. Like `Money.CreateExact`, but it also counts the decimal places the amount was written with, trailing zeros included, so `1.500` EUR is refused instead of saved as 1.50 (it is most likely 1500 typed with a thousands separator).

A `decimal` keeps the decimal places it was written with: `decimal.Parse("1.500")` and the JSON number `1.500` read by
System.Text.Json have three, which `CreateExact` accepts because the value fits the currency. `CreateFromInput` refuses them and names
the currency's decimal places, with examples that have that many:

```csharp
var price = Money.CreateFromInput(request.BasePrice, Currency.Eur, field: "basePrice", label: "Base price");
// 1500    -> EUR 1500.00
// 1500.50 -> EUR 1500.50
// 1.500   -> "Base price must have at most 2 decimal places, like 1500 or 1500.50." on basePrice
```

A currency without decimals, such as JPY, refuses `1500.0` with `Base price must be a whole number, like 1500.`; one with three, such
as KWD, refuses `1.5000` with `... at most 3 decimal places, like 1500 or 1500.500.`.

Pass the amount as it was parsed. A computed amount such as `1000.00m * 0.30m` has four decimal places, so it belongs in `CreateExact`.

## Business time

`BusinessTimeProvider(businessTimeZone, inner = null)` is a `TimeProvider` that reports the configured business zone (for example `America/Sao_Paulo`) as its `LocalTimeZone`; UTC time and timers come from the inner provider (default `TimeProvider.System`). `GetLocalNow()` therefore follows the business day rather than the server's zone, and `provider.Today()` (`TimeProviderExtensions`) returns that local calendar day as a `DateOnly`, for example for `BirthDate.Create`. Register it as the app's `TimeProvider`.

## Concurrency conflicts

`ConcurrencyConflictException` signals that a save found the row changed or gone since it was read. Like `UniqueConstraintViolationException` it is persistence-neutral: the provider exception is kept as the inner exception and the message never contains data values. `SharedKernel.EntityFrameworkCore` raises it from `SaveChangesTranslatingConflicts`.
