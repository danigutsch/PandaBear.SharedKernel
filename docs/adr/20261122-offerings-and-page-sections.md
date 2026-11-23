# Offerings and page sections

Date: 2026-11-22. Status: accepted.

Both ViajantesTurismo applications sell tours and build their public pages from content sections. ViajantesTurismo V2 keeps a tour
model and a page-section model in its site context; the original application keeps a catalog tour and a plain-text page body. The two
tour models conflict: duration is nights in one and days in the other, publication is per aggregate in one and per translation in the
other, the itinerary is plain text in one and structured in the other, and one requires four fixed prices while the other has an
optional typed list. This record decides how SharedKernel models what they share. It follows
[ADR-022](20260621-split-sharedkernel-domain-and-building-blocks.md): each package owns one focused capability, and a primitive is shared
only when its boundary and current use are clear.

## Decision

- **Two model packages, each depending on BuildingBlocks only.** `SharedKernel.Offerings` (`PandaBear.Offerings`) models a sellable
  programme: schedule and availability, typed prices, group and payment conditions, metrics, language-neutral steps, images and
  translated text, with a public view, listing groups, display sentences and JSON-LD. `SharedKernel.PageSections`
  (`PandaBear.PageSections`) models typed page sections and their validation. They are separate because they have different
  consumers: a page that is not a tour uses sections, and the original application can adopt sections before its tours are localized.
  A tour page uses both, so neither references the other; the application composes them. Their renderers come later
  (`PageSections.Blazor`, and an `Offerings.Blazor` once both applications render the same tour template); the models reference no
  display package, as Galleries does.
- **The generic concept is an offering, not a tour.** No Tours package exists. What makes an offering a tour is application data:
  - its price kinds, an application enum with `PriceKindRule(Kind, Repeatable, NeedsLabel, IsBase)` rules whose order is the display
    order;
  - its metric kinds, another application enum with `MetricRule(Kind, Scope, Min, Max, Decimals, AllowsRange, AllowsApproximate)` rules.
  A tour's distance, elevation gain, transfer and difficulty are metrics. A metric is a decimal with an optional upper end and an
  approximate flag, at the offering's scope, a step's or both. Typed fields per tour fact were rejected because every new fact would
  change SharedKernel's public API. A string-keyed bag was rejected because it loses validation and order, and string member keys are
  avoided in this repository. What has no generic meaning stays in the application: V2's route cities, terrain note and extension flag.
- **Each application extends the shared models through type parameters, not inheritance.**
  - `Offering<TImageId, TPriceKind, TMetric>` takes the application's enums, which are stored by name.
  - `PageSection<TImageId, TLink, TExtra>` takes the application's validated link target and a struct extension whose `default` means
    none (`NoExtra` for applications without one). Definitions hold the raw link input instead (`TLinkInput`), and an
    `IPageSectionContext<TImageId, TLinkInput, TLink, TExtra>` turns it into the target and validates the extension, so an opaque target
    such as V2's `NavTarget`, V2's settings block and its tour-information condition stay in V2. The extension members have default
    bodies, so an application without an extension writes only the link check and its image knowledge.
  - Both read BuildingBlocks' `IImageEligibility<TImageId>`, which Galleries' `IAlbumPhotoEligibility` now derives from, so one image
    index serves every content rule.
- **Image primitives shared by both live in BuildingBlocks' `Media` folder**: `IImageEligibility` and, added with the owner's approval
  during the build, `ImageCredit` with `ImageLicense`, the attribution of a third-party image (title, author, source, licence and a
  change note, the Creative Commons practice). The credit is a value an application keeps on its image library, not a part of an
  offering or a section; its caption and credits-list components come with the display atoms.
- **The models own rules and views, never storage.** Like Galleries, there is no id, version, persistence, events or authorization.
  Create and Revise validate and return every error at once on stable field paths. `ToDefinition` and a structural `Restore` let a
  relational mapper or an event-sourced aggregate store the definition, so nothing assumes EF Core or mutable entities.

## Deviations from the design proposal

The owner approved the proposal (2026-11-22) and these corrections, found while building it:

- `Offering.Restore(definition, rules)` takes the rules instead of only the cultures. The from price and the labelled prices need each
  kind's `IsBase` and `NeedsLabel` flags, which only the rules hold, and the stored definition carries only kind and amount. Restore
  stays structural: from the rules it reads only those flags. The offering keeps them, so on replay the from price and the labels
  follow the current rules.
- A section's button style is `LinkStyle` (Primary, Secondary, Outline; default Primary) in PageSections, not Blazor.Site's
  `ButtonVariant`, which a BuildingBlocks-only model cannot reference. The renderer maps one to the other.
- The renderer is `PageSectionsView<TImageId, TLink, TExtra>` in `SharedKernel.PageSections.Blazor`, not `PageSections`: a type of that
  name clashes with the `PandaBear.PageSections` namespace (CA1724), which the build treats as an error and this repository does not
  suppress.
- `ImagePicker<TKey, TOption>` in `SharedKernel.Blazor` takes the option's key from its `PickerCatalog` (built with the key function),
  so there is no separate `KeyOf` parameter that could disagree with the catalog. `SharedKernel.Blazor` references BuildingBlocks for
  `PageWindow` and the thumbnail check, as planned; its test project moves to the Web SDK to host an Interactive Server page for the
  picker's browser and prerendered-state tests, and runs in the browser slice.

## Enum zero values

`OfferingAvailability` and `OfferingCallToAction` have an explicit `Unspecified = 0` ("not set") and explicit values for every member,
following the .NET enum design guidelines and CA1008 (a simple enum has a zero value; the rule is not suppressed) and Google's AIP-126 and
AIP-216 (the zero of a state enum is "unspecified", accepted on input and never a valid state). Validation, Restore, the structured-data
option, the text helpers and the listing refuse it, every switch over these enums ends in a throwing arm, and a view's call to action is
never `Unspecified`. Applications serialize them by name, with `JsonStringEnumConverter<TEnum>` and `allowIntegerValues: false`.

## Consequences

- Two more preview packages with public API baselines. No new NuGet dependency: JSON-LD is built with `System.Text.Json.Nodes`, without
  reflection, so it stays trim- and NativeAOT-compatible.
- Both applications can map onto the offering model: the tests cover the original application's single-language scheduled tour with
  four required prices and nights-based admin duration (duration comes from `DateOnlyRange.Days`), and V2's five price kinds with
  per-language labels for its "other" kind. Per-aggregate publication and review stay in the original application, on top of
  published translations.
- All words are the application's: the date patterns, plural forms and sentence formats are parameters, and validation messages are
  English and never echo input, to be mapped to the editor's copy.

[Decision index](../README.md).
