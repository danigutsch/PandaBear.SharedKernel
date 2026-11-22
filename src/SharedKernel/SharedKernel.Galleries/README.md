# PandaBear.SharedKernel.Galleries

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Photo albums for a public site: an ordered sequence of distinct photos with a cover, a sort order, an optional related item such as a
tour, and a title, slug and publication status per culture, with a publish rule, change detection and a paged public view. It depends
only on `PandaBear.SharedKernel.BuildingBlocks` (`ContentCulture`, `SupportedCultures`, `TranslationSet`, `Slug`, `PageWindow`, `PublicationStatus`) and
keeps no persistence, concurrency, events or authorization: the application's own entity owns those. Image storage, processing and
renditions stay with the application too; the album holds photo ids only.

## Install

Replace the placeholders with an actually validated candidate version and its local/private feed. Registry publication remains deferred.

```text
dotnet add package PandaBear.SharedKernel.Galleries --version <validated-candidate-version> --source <candidate-feed>
```

## Model

`Album<TPhotoId, TRelatedId>` is generic over the photo id and the related item id, both value types (`Guid` in both current
applications):

| Member | Meaning |
| --- | --- |
| `SortOrder` | Position among the albums, lower first; the album id breaks ties (`AlbumOrdering.InDisplayOrder`, or `OrderBy(a => a.SortOrder).ThenBy(a => a.Id)` in a query) |
| `RelatedId` | Optional related item, such as the tour the album shows |
| `PhotoIds` | The photos in display order, each once, at most `AlbumRules.MaxPhotos` |
| `CoverId`, `EffectiveCoverId` | The cover the editor chose, which must be one of the photos; without one the first photo is the cover |
| `Translations` | One `AlbumTranslation` per culture: `Culture`, `Title`, `Slug`, `Status` (`PublicationStatus.Draft` or `Published`, from BuildingBlocks) |

`AlbumRules` holds the application's choices: the `SupportedCultures` an album is written in (a translation in the default culture is
required), `MaxPhotos`, and the optional `MaxTitleLength` (default 150) and `MaxSlugLength` (default `Slug.DefaultMaxLength`, 128).

`Album.Create(definition, rules, photos)` validates an `AlbumDefinition` (the entered data, with `AlbumTranslationDefinition`s) and
returns the album or every error at once. `album.Revise(definition, rules, photos)` validates the same way and returns `Result<bool>`:
whether anything changed, so the application bumps its version only then; on failure the album is unchanged. The order of the
translations counts as data: the public view's same-language fallback (`pt` finding `pt-BR` before `pt-PT`) takes the first one given,
so reordering them is a change and is kept. `Translations` and `PhotoIds` are read-only copies.

`Album.Restore(...)` and `AlbumTranslation.Restore(...)` rebuild a stored album for a persistence mapper. They do not run the album
rules (lengths, the photo maximum, the publish rule), so never use them for input, but they refuse what later reads cannot handle: a
missing value, two translations of one culture, a cover that is not one of the photos (`ArgumentException`), and an undefined status
(`ArgumentOutOfRangeException`). A bad row then fails in the mapper, not on every page that lists the album.

A blank slug is derived from the title with `Slug.CreateLenient` (`Viagem à Patagônia, 2027!` gives `viagem-a-patagonia-2027`); a given
slug must pass `Slug.Create`. A title without an ASCII letter or digit (`東京`) needs an explicit slug. Slugs need not be unique across
the cultures of one album; uniqueness across albums is a database concern of the application, as are slug history and redirects.

### Publish rule

A translation can be `Published` only when the album has at least one photo and every photo is eligible in its culture. The application
decides eligibility through `IAlbumPhotoEligibility<TPhotoId>`: `Exists(photoId)` and `IsEligible(photoId, culture)`. One application asks
for alt text, or a decorative mark, in that culture; another asks that the photo is processed and its text reviewed. The rule runs on
every `Create` and `Revise`, so editing photos cannot leave a published translation with an ineligible photo. The validation error only
counts the blocking photos; `AlbumPublishing.FindIneligible(photoIds, culture, photos)` lists them, in album order, so an editor can
name them (for example by file name).

### Validation errors

Errors are `Result`/`ValidationErrors` with field paths an editor can map to its controls (`AlbumFields`); a translation's fields are
prefixed with its culture (`AlbumFields.Of(culture, AlbumFields.Title)` is `pt-BR.title`). The messages are English and never echo the
input; map the fields to your own copy when the editor speaks another language.

| Field | Errors |
| --- | --- |
| `photoIds` | more than `MaxPhotos`; a photo that does not exist; a photo given twice |
| `coverId` | the cover is not one of the photos |
| `translations` | a culture outside `rules.Cultures`; a culture given twice; no translation in the default culture |
| `{culture}.title` | not valid text (malformed UTF-16); missing; longer than `MaxTitleLength` (after sanitizing) |
| `{culture}.slug` | not a valid slug; longer than `MaxSlugLength`; blank with a title that gives no slug |
| `{culture}.status` | not a defined `PublicationStatus` |
| `{culture}.photos` | published without a photo; published with photos that are not eligible in the culture |

## Public view and pages

`album.PublishedIn(requestedCulture, cultures)` builds the `AlbumView` a public page shows: the published translation of the requested
culture, its neutral parent or another culture of its language, else that of the default culture, through `TranslationSet.TryResolve`.
It returns `null` when no published translation applies (answer 404 Not Found). `view.Culture` tells which culture the text is in, so the
page can mark text of another language with `lang`. The view carries `Title`, `Slug`, `SortOrder`, `RelatedId`, `PhotoIds`, `PhotoCount`
and the effective `CoverId`.

`view.Page(requestedPage, pageSize)` slices one page of photos with `PageWindow` (see the BuildingBlocks README): pass the raw `page`
query value, and answer 404 when `page.Window.IsOutOfRange`. `page.PhotoIds` are the photos to render; turn them into `GalleryImage`s
with the application's renditions and give them, with `page.Window`, to `AlbumSection` in `PandaBear.SharedKernel.Blazor.Site`.

```csharp
var rules = new AlbumRules(SiteCultures.Supported, maxPhotos: 200) { MaxTitleLength = 120 };

var created = Album.Create(new AlbumDefinition<Guid, Guid>(0, tourId, photoIds, null,
    [new AlbumTranslationDefinition("pt-BR", "Patagônia", null, PublicationStatus.Published)]), rules, imageIndex);

var view = album.PublishedIn(requestedCulture, SiteCultures.Supported);
if (view is null || view.Page(pageQuery, pageSize: 24) is not { Window.IsOutOfRange: false } page)
{
    return NotFound();
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Galleries`. C# namespace: `PandaBear.Galleries`. Assembly/project identity: `SharedKernel.Galleries`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
