# PandaBear.SharedKernel.Offerings

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Sellable programmes for a public site, such as tours: availability and dates, priced lines in the application's own price kinds,
group and payment conditions, application-defined metrics, language-neutral steps, images, and translated text with a publish rule per
culture; a public view per culture, listing groups, display sentences and schema.org JSON-LD. It depends only on
`PandaBear.SharedKernel.BuildingBlocks` and keeps no id, version, persistence, events or authorization: the application's own entity
holds the offering and owns those concerns. Display components are not part of it; the Blazor.Site atoms take its view's values.

## Install

Replace the placeholders with an actually validated candidate version and its local/private feed. Registry publication remains deferred.

```text
dotnet add package PandaBear.SharedKernel.Offerings --version <validated-candidate-version> --source <candidate-feed>
```

## Model

`Offering<TImageId, TPriceKind, TMetric>` is generic over the image id (`TImageId : struct, IEquatable<TImageId>`) and two enums of the
application: its price kinds and its metric kinds. Alias the closed type once and store the enums by name:

```csharp
using TourOffering = SharedKernel.Offerings.Offering<System.Guid, TourPriceKind, TourMetric>;
```

| Member | Meaning |
| --- | --- |
| `SortOrder` | Position among the offerings, lower first |
| `Schedule` | `OfferingSchedule(Availability, Dates, Year, DurationDays)` with `Nights = DurationDays - 1` (see [Availability](#availability)) |
| `SignUpLink` | Optional `WebUrl`; the view shows it only for a scheduled offering |
| `Currency`, `Prices`, `FromPrice` | The prices as `PriceLine(Kind, Money)` in the rules' kind order; the from price is the lowest price of a base kind |
| `Participants`, `GroupCondition`, `PaymentTerms` | `ParticipantRange(Min?, Max?)`; `GroupCondition(MinimumParticipants, SurchargePerMissing, DueDays)`; `PaymentTerms(DepositPercent?, Instalments?, BalanceDueDays?)` |
| `Metrics` | The offering's `Metric(Kind, Value, Upper?, Approximate)` in the rules' order, such as a tour's distance and difficulty |
| `Steps` | Language-neutral `ProgramStep(ImageId?, Metrics)`, such as the days of an itinerary; their text is per translation |
| `CoverId`, `GalleryIds`, `ImageIds` | The cover, the gallery, and every image shown (cover, gallery, step images, each once) |
| `Translations` | One `OfferingTranslation` per culture, in the order given: the order decides the same-language fallback |

A translation holds `Culture`, `Status` (`PublicationStatus`), `Slug`, `Title`, `Subtitle` (a tagline only), `Eyebrow` (a short line
above the title, such as a tour's countries), `Summary`, `Description` (paragraphs), `DateNote`, `SeoTitle`, `SeoDescription`, four
one-line-per-item lists (`Highlights`, `Included`, `NotIncluded`, `PracticalInfo`), `PriceLabels` (one per price of a kind that
`NeedsLabel`: entered in the order those prices are entered, blank for a missing one, and moved with their prices when the prices are
sorted into the kind order; `null` marks a missing label) and `StepTexts` (`ProgramStepText(Title, Paragraphs, Note)`, the note being free text such as "+ barco").

`OfferingRules<TPriceKind, TMetric>` is the application's policy: the `SupportedCultures` (a translation in the default culture is
required), the `PriceKindRule(Kind, Repeatable, NeedsLabel, IsBase)`s and `MetricRule(Kind, Scope, Min, Max, Decimals, AllowsRange,
AllowsApproximate)`s, whose list order is the display order, and limits with defaults: `MaxDurationDays` 365, `MinYear`/`MaxYear` 2000
and 2100, `MaxTitleLength` 200, `MaxLineLength` 300, `MaxSummaryLength` 1000, `MaxDateNoteLength` 100, `MaxSlugLength` 128,
`MaxParagraphs` 50 of `MaxParagraphLength` 4000, `MaxPrices` 20, `MaxListItems` 60, `MaxSteps` 60, `MaxGalleryImages` 60,
`MaxParticipants` 200 and `MaxPrice` 1,000,000. `StepCount` is `Free` (default) or `MatchesDuration` (no steps, or one per day),
`HighlightsCount` an optional `CountRange` (none, or that many), and `ForbiddenStepTitlePrefixes` words such as `Dia` and `Day` that a
step title must not begin with when a number follows, because the number comes from the position.

A tour is an offering whose metrics are its distance, elevation and difficulty: there is no Tours package. ViajantesTurismo V2 configures:

```csharp
var rules = new OfferingRules<TourPriceKind, TourMetric>(SiteCultures.Supported,
    [
        new(TourPriceKind.PerPersonDoubleRoom, IsBase: true),
        new(TourPriceKind.SingleRoomSupplement),
        new(TourPriceKind.BikeRental),
        new(TourPriceKind.EBikeRental),
        new(TourPriceKind.Other, Repeatable: true, NeedsLabel: true),
    ],
    [
        new(TourMetric.Distance, MetricScope.Offering | MetricScope.Step, 0m, 100_000m, Decimals: 1, AllowsApproximate: true),
        new(TourMetric.Transfer, MetricScope.Step, 0m, 100_000m, Decimals: 1),
        new(TourMetric.ElevationGain, MetricScope.Step, 0m, 10_000m),
        new(TourMetric.ElevationLoss, MetricScope.Step, 0m, 10_000m),
        new(TourMetric.Difficulty, MetricScope.Offering, 1m, 5m, AllowsRange: true),
    ])
{
    StepCount = OfferingStepCount.MatchesDuration,
    HighlightsCount = new CountRange(3, 10),
    ForbiddenStepTitlePrefixes = ["Dia", "Day"],
};
```

### Create, revise, store and restore

`Offering.Create(definition, rules, images)` validates an `OfferingDefinition` (the entered data, with an `OfferingScheduleDefinition`,
`PriceLineDefinition`s, a `GroupConditionDefinition`, `ProgramStepDefinition`s and `OfferingTranslationDefinition`s with
`ProgramStepTextDefinition`s) and returns the offering or every error at once. `offering.Revise(definition, rules, images)` validates
the same way and returns `Result<bool>`: whether the data changed, so the application bumps its version only then; on failure the
offering is unchanged. `images` is the application's `IImageEligibility<TImageId>` from BuildingBlocks, the same one albums read.

`offering.ToDefinition()` gives the data back as a definition that creates or restores the same offering. It is the only form to
store, in an event or a row, after `Create` or `Revise`: never store the entered definition, which is not sanitized, sorted or
completed (labels follow their sorted prices, slugs are derived). A dated offering's year is left out (it comes from the dates), and
paragraphs are joined with blank lines.

`Offering.Restore(definition, rules)` rebuilds a stored offering for a persistence mapper or event replay. It is structural only: it
refuses what later reads cannot handle (`ArgumentException`: an undefined availability, price kind, metric kind or status, two
translations of one culture, a malformed culture, currency code or slug, dates that are not a range, an undated scheduled or sold-out
offering, an undated offering to be confirmed without a year, a duration below one day, prices or a group condition without a currency,
an incomplete group condition, a missing title) and runs no lengths, limits or publish rules, so
never use it for input. From the rules it reads only the price-kind flags `IsBase` and `NeedsLabel`. The offering keeps those flags, so
on replay the from price and the labelled prices follow the current rules; a stored culture that the rules no longer list is kept.

### Availability

`OfferingAvailability` is required. Its zero value is `Unspecified` ("not set", see [Enums](#enums)), so a missing availability is an
error and never means `Scheduled`.

| Availability | Dates | Year | Date note | Call to action | schema.org |
| --- | --- | --- | --- | --- | --- |
| `Scheduled` | required | from the first day; a given year is refused | refused | sign up, when it has a link | `InStock` |
| `SoldOut` | required | from the first day | refused | contact | `SoldOut` |
| `DatesToBeConfirmed` | optional | required when undated; from the first day when dated | only when undated | contact | `PreOrder` |
| `OnRequest` | refused | refused | allowed | contact | `MadeToOrder` |

The duration is required even without dates, 1 to `MaxDurationDays`, and equals the dates' day count, both days included, when dated. A
sign-up link on an offering that is not scheduled is kept and hidden.

### Rules

All errors are returned at once, with English messages that never echo the input:

- prices: a kind in the rules, a non-repeatable kind at most once, amounts through `Money.CreateFromInput`, positive and at most
  `MaxPrice`, at most `MaxPrices`; the currency is required with prices or a group condition;
- group condition: all three values together, the minimum 1 to the largest group (`Participants.Max`, else `MaxParticipants`; it is
  separate from the smallest group), the surcharge positive money in the offering's currency, due days 1 to 365;
- payment terms, each optional: deposit 1–100 %, instalments 1–24, balance 1–365 days; group sizes 1 to `MaxParticipants`, the largest
  not below the smallest;
- metrics: a kind in the rules allowed at that scope (offering or step), once each, within `Min`–`Max` with at most `Decimals`; an upper
  value needs `AllowsRange` and lies above the value, `Approximate` needs `AllowsApproximate`;
- images: the cover and step images exist; the gallery's images exist, each once, at most `MaxGalleryImages`;
- steps: at most `MaxSteps`, and with `MatchesDuration` none or one per day; a translation has at most one text per step, and a step
  title must not begin with a forbidden prefix followed by a number;
- translations: a culture of the rules, once each, the default culture required; a blank slug is derived from the title with
  `Slug.CreateLenient`; highlights none or within `HighlightsCount`; at most as many price labels as labelled prices;
- publishing a translation needs a summary, a description, every price label, a text for every step, and every image (cover, gallery
  and step images) eligible in its culture. `OfferingPublishing.FindIneligible(offering.ImageIds, culture, images)` lists the blocking
  images; the error only counts them.

### Validation errors

Errors are `Result`/`ValidationErrors` at the paths of `OfferingFields`; a translation's fields are prefixed with its culture through
`OfferingFields.Of(culture, field)`, and metric paths name the kind in camelCase.

| Field | Errors |
| --- | --- |
| `availability`, `startDate`, `endDate`, `year`, `durationDays` | the availability table and the duration |
| `signUpLink`, `currency` | not a web address; missing, or not an ISO 4217 code |
| `prices`, `prices[i].kind`, `prices[i].amount` | too many; a kind outside the rules or repeated; an amount that is not positive money up to the maximum |
| `participants.min`, `participants.max` | outside 1 to `MaxParticipants`; the largest below the smallest |
| `groupCondition.minimumParticipants`, `.surchargePerMissing`, `.dueDays` | missing one of the three; out of range |
| `paymentTerms.depositPercent`, `.instalments`, `.balanceDueDays` | out of range |
| `metrics`, `metrics.{kind}`, `metrics.{kind}.upper` | a kind outside the rules; scope, repetition, bounds, decimals, approximate; the range |
| `steps`, `steps[i].imageId`, `steps[i].metrics…` | too many or not one per day; a missing image; the step's metrics as above |
| `coverId`, `galleryIds`, `translations` | a missing image; too many, missing or repeated gallery images; the cultures |
| `{culture}.title`, `.slug`, `.status`, `.subtitle`, `.eyebrow`, `.summary`, `.description`, `.dateNote`, `.seoTitle`, `.seoDescription` | text rules; the date note when dated; summary and description to publish |
| `{culture}.highlights`, `.included`, `.notIncluded`, `.practicalInfo`, `.priceLabels` | empty, long or too many items; the highlights range; labels too many, or missing to publish |
| `{culture}.steps`, `.steps[i].title`, `.steps[i].text`, `.steps[i].note` | more texts than steps, or missing to publish; the title prefix and text rules |
| `{culture}.images` | images not eligible in the culture, to publish |

## Enums

`OfferingAvailability` and `OfferingCallToAction` give every member an explicit value, because applications persist and serialize
them, and their zero is `Unspecified`, which means "not set":

- inputs (a definition, a stored row, an event) may carry `Unspecified`, but a validated `Offering` never does: `Create` refuses it with
  `Availability is required.` on `availability`, `Restore` refuses it with an `ArgumentException`, and the structured-data option, the
  text helpers and the listing refuse it as invalid input; a view's call to action is always `SignUp` or `Contact`;
- serialize them by name, with System.Text.Json's `JsonStringEnumConverter<TEnum>` (trimming- and NativeAOT-safe) and
  `allowIntegerValues: false`, so a numeric payload such as `0` is refused instead of read;
- persistence stores the names, or the explicit numbers, never positions.

## Public view and listing

`offering.PublishedIn(requestedCulture, cultures)` builds the `OfferingView` of one whole published translation through
`TranslationSet.TryResolve`: the exact culture, its neutral parent or another culture of its language, else the default culture. It
never mixes the fields of two translations, and `view.Culture` tells which one it is so the page can mark it with `lang`. The view carries
the text (`SeoTitle` falls back to the title and `SeoDescription` to the summary), the schedule fields, `CallToAction` (`SignUp` only for
a scheduled offering with a link, else `Contact`; never `Unspecified`) and `SignUpLink` (only with `SignUp`), the prices as `PriceView(Kind, Amount, Label,
IsBase)` with this culture's labels, `FromPrice`, the conditions, the metrics, the steps as `ProgramStepView(Number, Title, Paragraphs,
Note, ImageId, Metrics)`, the lists and the images. It returns `null` when no published translation applies.

`OfferingListing.Group(views)` returns `OfferingGroup(Kind, Year, Items)`s: one `Year` group per year, ascending, for the scheduled,
sold-out and to-be-confirmed offerings, then one `OnRequest` group; within a group by sort order, then first day, undated last. An
overload groups the application's own items through a sort-order and a schedule selector.

## Display sentences

`OfferingText` builds sentences from the application's formats; every word is the application's copy and plurals have two forms
(`PluralFormat(One, Other)`). For a public view, pass the culture of `view.Culture` (the text shown, which differs from the requested
one after a fallback) with that language's formats, so the sentences match the text:

- `DateLine(schedule, dateNote, patterns, toBeConfirmedFormat, culture)`: the dates through `DateOnlyRange.ToDisplayString` (one format
  for the card and the page, `20 a 29 de setembro de 2027`); else the note; else `Datas a confirmar, 2028` for an undated offering to
  be confirmed; else `null` for one on request, whose badge says it;
- `Duration(schedule, days, nights, culture)`: `["8 dias", "7 noites"]`; a one-day offering has no nights part;
- `Participants(range, ParticipantFormats, culture)`, `GroupCondition(condition, GroupConditionFormats(Sentence, Minimum, DueDays),
  MoneyDisplayOptions, culture)` (`{0}` the minimum in its plural form, `{1}` the surcharge as money for people, `{2}` the due days in
  their plural form, `1 dia`/`45 dias`) and `PaymentTerms(terms, PaymentTermsFormats, culture)`;
- `Metric(metric, MetricFormat(Pattern, ApproximatePattern, Decimals), culture)`: `aprox. 320,5 km`, a range with an en dash (`2–3`).

## Structured data

`OfferingStructuredData.Build(view, options)` returns a `JsonObject` built with `System.Text.Json.Nodes`, without reflection, so it works
under trimming and NativeAOT. It marks up only what the page shows: a `TouristTrip` (or `Product`) with the name, description, `url`
(`CanonicalUrl`), `image` (`ImageUrls`), a tourist trip's `provider` (`StructuredDataOrganization`), an `Offer` at the from price with
the availability and one `UnitPriceSpecification` per price, named by the price's own label or `PriceLabel(kind, culture)`, called with
`view.Culture` (an `AggregateOffer`
with `lowPrice`, `highPrice` and `offerCount` when several prices are of a base kind), and a tourist trip's `itinerary` as an
`ItemList` of its steps. Without a from price there is no offer. `Availability` overrides the offering's own; an undefined value is refused. Departure and arrival times
are left out, since an offering has days, not times. Render it in a `<script type="application/ld+json">` with an encoder that escapes
`<`, such as `JavaScriptEncoder.Default`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Offerings`. C# namespace: `SharedKernel.Offerings`. Assembly/project identity: `SharedKernel.Offerings`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
