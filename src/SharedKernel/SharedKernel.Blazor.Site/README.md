# SharedKernel.Blazor.Site

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Brand-neutral, accessible Razor components for a public website, plus one stylesheet of design tokens. The components use semantic
landmarks, need no JavaScript and no interactive render mode, and work with static server-side rendering. Every component except
`PageHead` takes a `class` and passes any other unmatched attribute to its root element. An unmatched name that starts with an
upper-case letter, names a removed parameter in any case (`ImageSrc`, `ImageAlt`, `ImageWidth`, `ImageHeight`: use `Image`), or
holds a character an HTML attribute name cannot hold throws `InvalidOperationException` naming it, so a misspelled or removed
parameter fails instead of silently becoming an attribute. Write pass-through HTML attributes in lower case.

## Usage

Reference the stylesheet from the host page, then your own stylesheet that overrides the tokens. The host also sets
`<html lang>`, because a component cannot, and renders `<HeadOutlet />` for `PageHead`.

```html
<html lang="pt-BR">
<head>
    <link rel="stylesheet" href="_content/PandaBear.SharedKernel.Blazor.Site/site.css" />
    <link rel="stylesheet" href="brand.css" />
    <HeadOutlet />
</head>
```

```razor
@using PandaBear.Blazor.Site

<SkipLink>Skip to content</SkipLink>
<SiteHeader Label="Main" Items="_nav" CurrentPath="@Navigation.ToBaseRelativePath(Navigation.Uri)">
    <Brand><a href="/">Brand</a></Brand>
    <Actions><LanguageSwitcher Label="Language" Languages="_languages" /></Actions>
</SiteHeader>
<main id="main-content" tabindex="-1">
    <Hero Eyebrow="Since 2004" Title="Small-group tours" Subtitle="Eight countries, one guide."
          Image="@ResponsiveImage.Create(LinkTarget.FromString("/img/hero.jpg"), 1600, 900, ImageAlternative.Describe("A lake below snowy peaks"))">
        <Actions><ButtonLink Href="@LinkTarget.FromString("/tours")">See the tours</ButtonLink></Actions>
    </Hero>
    <Section Title="Tours" HeadingId="tours-heading" Intro="Pick your next trip.">
        <CardGrid>
            <Card Title="Atacama" Href="@LinkTarget.FromString("/tours/atacama")"
                  Image="@ResponsiveImage.Create(LinkTarget.FromString("/img/atacama.jpg"), 800, 600, ImageAlternative.Decorative())">
                <p>Eight days in the desert.</p>
            </Card>
        </CardGrid>
    </Section>
</main>
<SiteFooter><ContactCard Email="_email" Phone="_phone" PhoneText="+55 (11) 99999-0000" WhatsApp="_phone" /></SiteFooter>

@code {
    private static readonly SiteNavItem[] _nav =
    [
        new("Tours", LinkTarget.FromString("/tours")),
        new("About", Children: [new("Team", LinkTarget.FromString("/about/team")), new("Story", LinkTarget.FromString("/about/story"))]),
    ];
}
```

The stylesheet is a static web asset. Its path is `_content/` plus the package id plus `/site.css`:
`_content/PandaBear.SharedKernel.Blazor.Site/site.css`. An app that maps assets with `MapStaticAssets` can write
`@Assets["_content/PandaBear.SharedKernel.Blazor.Site/site.css"]` to get the fingerprinted address.

## Components

| Component | Renders |
| --- | --- |
| `SkipLink` | The first link of the page; `TargetId` defaults to `main-content`. Extra attributes cannot replace its `href` or `class`. |
| `SiteHeader` | `header` with a brand slot, a labelled `nav` (`Label` is required when there are items), and an actions slot (`site-header__actions`). An item has either an `Href` or one level of children; an item with children is a native `details` that starts closed and shows a chevron that turns when it opens. The item that matches `CurrentPath` gets `aria-current="page"`, and the summary of its group gets `aria-current="true"` and the current item's style, so the group stays closed and does not cover the page. Below the `48rem` breakpoint an open group stacks the navigation on a row of its own, the group's links under its summary; from the breakpoint on they drop down over the page. `MenuLabel` collapses the navigation and the actions behind a menu button below the breakpoint, and `Placement` lays the header over a cover hero; see [Menu](#menu) and [Bands and photos](#bands-and-photos). The brand slot lays its link out as a box around the logo (`display: flex`), so the link's focus ring surrounds the logo. A `LanguageSwitcher` in the actions takes the navigation role's type (`--site-nav-*`), so its links sit on the line of the navigation links. |
| `SiteFooter` | `footer` with a content slot and an optional `Legal` line. |
| `Hero` | `Layout` `Split` (the default: text and image side by side from the breakpoint on) or `Cover` (the text centred over the image; see [Bands and photos](#bands-and-photos)). A cover's `CoverHeight` is `Page` (the default: at least 20rem below the breakpoint, 34.375rem from it on) or `Landing` (`site-hero--landing`: at least 20rem below the breakpoint, from it on 85% of the small viewport height between 34.375rem and 52rem); content can make either taller, and `Landing` on a split hero throws `ArgumentException`. Only a cover with an `Image` keeps that minimum height, which exists to show the photo; without one the primary band is as tall as its content and padding, so a not-found or error page gets a compact band instead of an empty block. The page's one `h1`, an optional `Eyebrow` line above it and `Subtitle` line under it, an image loaded eagerly with `fetchpriority="high"`, and an actions slot. With an eyebrow or a subtitle the heading is an `hgroup` (`site-hero__heading`): the eyebrow `p` (`site-hero__eyebrow`), the `h1` (`site-hero__title`), then the subtitle `p` (`site-hero__subtitle`), so reading order matches visual order and neither line enters the outline; with neither it is the `h1` alone. |
| `Section` | `section` with a heading (`Level`, default 2), a decorative divider and an intro. An optional `Eyebrow`, a short label such as a tour's category, goes above the heading: the heading is then an `hgroup` (`site-section__heading`) of the eyebrow `p` (`site-section__eyebrow`) and the heading (`site-section__title`), so the eyebrow is read first and stays out of the outline and the section's name; the divider and intro follow the `hgroup`. Without an eyebrow (null or blank) the heading stands alone. The eyebrow is small, bold, letter-spaced text in the primary colour, and the on-primary colour in a `Primary` or `Image` band. Set `HeadingId` to give the heading that id and label the section by it (`aria-labelledby`), which makes it a named region; without it no id is generated, so prerendered and interactive markup match. `Media` makes its content a `Split`: the heading, intro and body in `site-split__main`, then the media in `site-split__media`. `TitleLanguage`, a BCP 47 tag such as `en`, marks the heading alone with `lang` when the title is in another language than the page, so the intro and body keep the page's language (WCAG 3.1.2). `TextAlignment` `Start` (the default) or `Center` (`site-section--text-center`) aligns the heading, divider, intro and body text; centred, the divider, paragraphs, a byline, a video link and an image narrower than its column are centred too, and a quote drops its start rule. The intro and the paragraphs and lists directly in the body keep a readable measure of `--site-text-width` (65ch) instead of running the content width. A section inside a section, such as a day of an itinerary, takes the full width of the outer body without vertical padding and is spaced from what comes before it by `--site-space-5`, so the spacing of nested sections does not add up. An undefined alignment throws `ArgumentOutOfRangeException`. |
| `Split` | Text (`ChildContent`) beside media (`Media`, required: `ArgumentNullException` without it), without a heading of its own: a plain `div`, never a landmark, so a heading-less band can put text and photos side by side. The text is in `site-split__main` and the media in `site-split__media`, two equal columns from the `48rem` breakpoint on and the media after the text below it, so reading order matches visual order; the media fills its column. `MediaSide` `End` (the default) puts the media column on the inline-end side; `Start` (`site-split--media-start`) puts it on the inline-start side from the breakpoint on, by grid order only, so the text stays first in reading and focus order and below the breakpoint the media still follows the text; alternate it to zigzag a day-by-day itinerary. `Alignment` `Start` (the default) starts both columns at the top, so a photo stays beside the heading and first paragraph it illustrates instead of floating halfway down a long text; `Center` (`site-split--align-center`) centres the columns on each other, for a short tagline beside a photo. Start is the default because the usual split is text-heavy: a tour description or an itinerary day beside one or a few photos. An undefined side or alignment throws `ArgumentOutOfRangeException`. `Section.Media` uses it with the default alignment, so both are one layout. |
| `Band` | A full-width `div` (never a landmark) around content of the content width: a `Section`, or content without a heading such as a tagline and a `Byline`. `Tone` is `Default`, `Muted` (surface-alt), `Primary` (primary background, on-primary text) or `Image` (a lazy photo darkened by the normal-text scrim, on-primary text), which requires `Image`; an `Image` on another tone throws `ArgumentException`. A section inside a band loses its own vertical padding and keeps its semantics. `TextAlignment` `Center` (`site-band--text-center`) centres the band's text and a section in it as `Section.TextAlignment` does; paragraphs directly in the band keep the `--site-text-width` measure. |
| `Byline` | A person: a lazy round photo (`Photo`, usually decorative), the `Name` and the `JobTitle`, each a `p`. |
| `VideoLink` | A link to a video on its own site instead of an embedded player: a lazy, decorative and self-hosted `Poster` (a described or remote poster throws `ArgumentException`), a play mark on the normal-text scrim, the visible `Title`, and a visually hidden `SiteHint` such as "(opens YouTube)"; the link's name is the title then the hint. `Href` must be an address that `WebUrl.CreateHttps` accepts (absolute `https`); the link carries `rel="noreferrer"`. No frame, no script, and no request to the video's site before the visitor's click. The poster is cropped to 16:9 (`object-fit: cover`), which cuts away the black bars of a 4:3 video thumbnail, and the link is at most `--site-video-width` (40rem) wide, so a 480 by 360 thumbnail is not blown up to the content width; in a centred section it is centred. |
| `FloatingLink` | A round link fixed in the bottom corner, such as a chat link: the icon in `ChildContent` is hidden from assistive technology and the required `Label` names the link in visually hidden text. While it is on the page, focused elements scroll clear of it (`scroll-padding`) and the footer, or the body without a footer, ends with room for it, so it never covers the end of the page. Its hover grows it with a transition that `prefers-reduced-motion` turns off. Below the `48rem` breakpoint, where it lies over the text, it is `--site-floating-link-size-narrow` (3rem, still above a 44px tap target) at `--site-space-2` from the corner, so it reaches 40px into the text column instead of 56px; no side gutter is reserved, because a gutter would narrow every line of a phone page for a button that covers a line at a time while the visitor scrolls. |
| `ButtonLink` | A link styled as a button (`Primary`, `Secondary`, `Outline`). `NewTab` adds `target="_blank"`, `rel="noopener noreferrer"` and a visually hidden `NewTabHint` after a space, so the link's name reads "Partner (opens in a new tab)" and not "Partner(opens in a new tab)". |
| `ActionRow` | A plain `div` (`site-action-row`, never a landmark) around actions such as `ButtonLink`s and links, in reading order, with a gap. `Justification` places them from the `48rem` breakpoint on in one wrapping row: `Start` (the default) together where reading starts, in line with the text above, so they read as one group; `Center` (`site-action-row--center`) together in the middle; `End` (`site-action-row--end`) together at the end; `Between` (`site-action-row--between`) spread across the width, for two actions on opposite sides such as previous and next. Below the breakpoint they are stacked at the start, in the middle (`Center` and `Between`) or at the end. An undefined justification throws `ArgumentOutOfRangeException`. A plain link in it keeps the link style in the primary colour. |
| `Card`, `CardGrid` | `article` with an optional lazy image, a heading (`HeadingLevel`, default 3) whose link covers the card, text and actions; an auto-fill grid whose columns are at least `--site-card-min-width` wide and that keeps empty columns, so a lone card has the width of one column instead of the whole row. Links, buttons, form controls, `summary` and `[tabindex]` elements in the body and the actions are raised above the covering link, so they stay clickable; any other interactive element you put in the body needs `position: relative; z-index: 1`. The focus ring is drawn on the card while a control in it has keyboard focus (`:has(:focus-visible)`). `TitleLanguage` marks the card's heading alone with `lang`, as on `Section`. `EagerImage` loads the image eagerly, for a card on the first screen. |
| `FactsTable` | `table` with a required `caption` and `th scope="row"` headers. |
| `FactGrid` | A `dl` (`site-fact-grid`) of `FactCard`s, such as a tour's basic information: each card is a `div` (`site-fact-grid__item`) of the `dt` label (`site-fact-grid__label`) and one `dd` per value (`site-fact-grid__value`), for example "8 days" and "7 nights". A value is a `FactValue`: its `Text` and an optional `Link` (`LinkTarget`, so a relative, `http`/`https`, `mailto:` or `tel:` address), which makes the text a plain `a` in the same tab, for example `new("info@example.org", LinkTarget.FromString("mailto:info@example.org"))`. A value wraps between words; in a word that holds an `@`, such as an e-mail address, the component writes a `wbr` before the `@` and every dot, so the address wraps between its parts (`info` / `.desk` / `@example` / `.org`) and never inside one, and other words, such as `8.5`, keep their dots unbroken. Only a part wider than its card breaks inside, so nothing scrolls sideways. A card's optional `Icon` is rendered first inside the `dt`, in a `span` (`site-fact-grid__icon`) with `aria-hidden="true"`, because a `div` in a `dl` may hold only `dt` and `dd` elements; the label alone names the term. The cards are centred and bordered like table cells, in wrapping rows (flex) that share the full width of the container. Below the `48rem` breakpoint as many cards at least 11rem wide fit a row as the width allows; from it on the row length is computed from the number of cards for balanced rows, as the class `site-fact-grid--columns-1` to `site-fact-grid--columns-4` (no inline style): as many as cards up to four, otherwise `ceil(n / ceil(n / 4))`, so 5 and 6 cards make rows of 3, 7 and 8 rows of 4, and 9 rows of 3. The cards of a shorter last row grow to fill it, so 7 cards are a row of 4 and a row of 3 wider cards, and a lone last card on a phone takes the whole row: no row leaves a notch. An empty grid has no column class; the label is bold and, like the icon, in the primary colour (the on-primary colour in a `Primary` or `Image` band). An `svg` icon without a `fill` attribute is filled with the colour; one with `fill="none"` keeps its strokes, so draw them with `currentColor`. A null card, a blank label, no values, a null value or a value with blank text throws `ArgumentException`. |
| `Disclosure` | Native `details` and `summary`; a tab substitute without script. A disclosure after other content, such as a `FactGrid`, keeps `--site-space-4` from it, and stacked disclosures keep `--site-space-2` between them. The summary is rounded like its box, so its focus ring follows the box's corners. |
| `Quote` | `figure` with a `blockquote` and a `cite` caption. In a `Primary` or `Image` band it drops its background and takes the on-primary colour, its rule included. |
| `ImageGrid` | `figure` per `GalleryImage` (a `ResponsiveImage` and an optional caption), lazy loading except the first `EagerCount`; `PrioritizeFirstImage` loads the first one eagerly with `fetchpriority="high"`. `Layout` `Grid` (the default) is rows of equal columns; `Mosaic` (`site-image-grid--mosaic`) is a collage of mixed aspect ratios: columns filled from top to bottom in document order, each image at its own aspect ratio, never cropped. In both, columns are at least 14rem wide and share the full width of the container, as many as there are images while they fit, so one image fills its container, in a section or in a `Split`'s media column. An image is at most 40rem or 80% of the viewport tall; a taller one is shown whole within that height (`object-fit: contain`), at the start of its box, in line with the text, or in the middle in a section or band with centred text. `Thumbnails` (`site-image-grid--thumbnails`) crops every image to one aspect ratio, `--site-thumbnail-aspect-ratio` (square by default, which crops portrait and landscape photos alike), in columns at least `--site-thumbnail-min-width` (6.5rem) and 15% of the width, so a phone shows two or three to a row and a desktop at most six; empty columns are kept, so a short last row keeps the thumbnails' size. Thumbnails show part of each photo: use them where the visitor browses many photos, and `Grid` or `Mosaic` where each photo must be seen whole. |
| `AlbumIndex` | A `CardGrid` (`site-album-index`) of `Card`s, one per `AlbumSummary`: the title as the heading linked to the album's page, a cover (the first `EagerCount`, default 4, eager), the photo count as the application words it (`site-album-index__count`), and `lang` on the title alone when it is in another language; see [Photo albums](#photo-albums). Nothing without albums. |
| `AlbumSection` | One page of an album: a `Section` whose heading id is `album-{slug}`, an `ImageGrid` of the page's photos in the `Layout` `Thumbnails` by default (the first `EagerCount` eager: by default 6 as thumbnails, 4 in a grid, 1 in a mosaic), optional content, then the `Pager`; see [Photo albums](#photo-albums). |
| `ContactCard` | `address` with postal lines and `tel:`, `mailto:` and `https://wa.me/` links; no `address` when there are no lines and no channels. `PhoneText` shows the number as written (`+55 (11) 99999-0000`) while the link uses the digits; it must be the same number as `Phone`. The WhatsApp link shows `WhatsAppLabel`. |
| `LanguageSwitcher` | Labelled `nav` of links with `hreflang`, `lang` and `aria-current="true"` on the current language. |
| `Pager` | A `nav` named by `Label` (`site-pager`) with plain links for a list paged through the URL, after the GOV.UK pagination component: previous (`rel="prev"`), the numbered pages and next (`rel="next"`); see [Pager](#pager). Nothing when there is one page. |
| `PageHead` | Title, description, canonical link, OpenGraph basics and `hreflang` alternates including `x-default`; each optional tag is left out when its parameter is unset. `Robots` writes `<meta name="robots">` (`noindex`, `noindex, nofollow`). An error or not-found page sets `Robots="noindex"` and no `CanonicalUrl`, `Alternates` or `DefaultAlternateUrl`, so it has no canonical, `og:url` or `hreflang` link. |

Content without a heading, such as a page's opening text or a contact block, goes in `<div class="site-container">`: the
`site-container` class gives it the content width and side gutter of `Section`, `Hero`, the header and the footer, so it lines up
with them, without a section's vertical padding. Space it from its neighbours yourself.

`SiteHeader.CurrentPath` matching compares paths only: the query and fragment are ignored, a missing leading slash is added
(`Navigation.ToBaseRelativePath(...)` returns `tours`, which matches `/tours`), a trailing slash is ignored, and the comparison is
ordinal and ignores case. The empty path matches `/`. An item with an absolute address never matches.

Texts that people read (labels, the menu button, the new-tab and video hints, the WhatsApp link text) are parameters so the caller can
localize them.

### Menu

With `MenuLabel` (for example "Menu") the navigation and the actions are the content of a `details` element whose `summary`, with
three bars before the label, is the menu button. The button, and below the header breakpoint the links and group summaries in the
menu, are at least 2.75rem (44px) tall, the target size of WCAG 2.2 SC 2.5.5. Below the `60rem` header breakpoint the button sits beside the brand, starts closed, and opens
the navigation as a column with the actions under it, in a row of its own; it works with the mouse, Enter and Space, and Chromium
exposes it as a disclosure with its expanded state. From the breakpoint on, a browser that supports `::details-content` (Baseline
2025: Chrome 131, Safari 18.4, Firefox 143) shows the content inline and hides the button, and an older browser keeps the button, so
the menu works everywhere without script or form. There is one `nav` in the document; while the menu is closed below the breakpoint it
is hidden, so assistive technology never meets it twice. `details`/`summary` was chosen over the Popover API (`popovertarget`): the
popover is Baseline 2025 too, but it opens centred in the top layer, and placing it under its button needs anchor positioning
(Baseline 2026), while a closed `details` falls back to a working button everywhere.

The header breakpoint is `60rem` (960px), above the `48rem` layout breakpoint: a header with a brand, five links and two language
links wraps to two rows at 800px, and the consumer's earlier site switched to its menu button below about 921px; 60rem is the next
round step above that, with room for labels that are longer in another language. One breakpoint serves the menu and the header over
the hero, so between 48rem and 60rem the header is the primary band with its menu button above the hero, and an open group stays in
the menu's column. A site's header that needs more room breaks to two rows above it; keep the navigation short.

### Pager

`Pager` follows the [GOV.UK Design System pagination component](https://design-system.service.gov.uk/components/pagination/) for
a list paged through the URL. It takes a `PageWindow` (`PandaBear.SharedKernel.BuildingBlocks`; see its README) and renders a `nav`
named by `Label` with a list of plain `a href` links, so crawlers follow them
([Google Search Central: pagination and incremental page loading](https://developers.google.com/search/docs/specialty/ecommerce/pagination-and-incremental-page-loading)),
it works in static server rendering, and an interactive page renders the same markup:

- previous (`rel="prev"`, absent on the first page) and next (`rel="next"`, absent on the last page), with `PreviousText` and
  `NextText` as their text;
- the numbered pages, bounded to the first, the last and `SurroundingPages` (default 1, at most 10) on each side of the current page;
  a larger gap is an ellipsis `li` (`site-pager__ellipsis`) with `aria-hidden="true"`, never a link, and a gap of one page shows
  that page;
- `aria-current="page"` on the current page, and with `PageLabelFormat` (for example `"Page {0}"`) an accessible name for every
  numbered link, the current one included;
- nothing at all when there is one page.

`PageHref` builds each page's `LinkTarget` from its number. Give page 1 the address without a page parameter, so the first page has
one canonical URL, and every other page its own URL, which `PageHead.CanonicalUrl` should repeat. Answer a request whose window
`IsOutOfRange` with 404 Not Found. Labels and link texts are your copy. The current page is the primary colour with on-primary text,
and the ellipsis is muted; each link is at least 2.75rem wide.

```razor
<Pager Window="_window" Label="Photo pages" PreviousText="Previous" NextText="Next" PageLabelFormat="Page {0}"
       PageHref="@(page => LinkTarget.FromString(page == 1 ? "/fotos" : $"/fotos?page={page}"))" />
```

### Photo albums

`AlbumIndex` and `AlbumSection` show a site's photo albums, one album page at a time, without script. They take display data only
(`AlbumSummary`, `GalleryImage`, `PageWindow`, `Slug`), so an application with AVIF, WebP and JPEG renditions and one with a single file
both fit: choosing renditions and media routes stays with the application, and so does every word on the page.

- `AlbumIndex` renders an `AlbumSummary(Title, Href, Cover, PhotoCountText, TitleLanguage)` per album as a card whose heading links to
  the album's page. The cover is usually decorative, because the title names the card. Word the count yourself, plural rules included
  (`"1 foto"`, `"12 fotos"`). `HeadingLevel` defaults to 3 under a section's `h2`.
- When an album's title fell back to another language than the page's, give that language's BCP 47 tag in `TitleLanguage` (on
  `AlbumSummary` and on `AlbumSection`). Only the title is in that language, so only the heading carries `lang`; the count, the cover's
  and photos' text, the pager and your own content keep the page's language (WCAG 3.1.2, language of parts). The tag is yours to choose:
  a content culture such as `pt` may declare `pt-BR` (`CultureLanguageTags.LanguageTagOf` in BuildingBlocks maps one to the other).
- `AlbumSection` renders one page of one album: a `Section` named by its heading, whose id is `album-` plus the slug, the same on every
  page and render, so `#album-patagonia` can be linked to; the page's photos in an `ImageGrid`; optional `ChildContent`, such as a link
  to the related tour; then the `Pager` (see [Pager](#pager)) with your `PagerLabel`, `PreviousText`, `NextText`, `PageLabelFormat` and `SurroundingPages`.
  Use `Level="1"` when the album is the page.

Images visible on the first screen load eagerly and the rest lazily, each with its width and height so the layout does not shift
([web.dev: browser-level image lazy loading](https://web.dev/articles/lazy-loading-images)). Both components count eager images in
document order:

- `AlbumIndex.EagerCount` defaults to `AlbumIndex.DefaultEagerCount`, 4: the first row of covers at the default content width, where
  72rem holds four 16rem card columns with their gaps.
- `AlbumSection.Layout` defaults to `Thumbnails`: a page of 24 photos in whole-photo columns at least 14rem wide was one photo per row and
  about 14,000px tall on a phone; as thumbnails it is three to a row. Set `Grid` or `Mosaic` to show each photo whole.
- `AlbumSection.EagerCount` left unset picks `AlbumSection.DefaultThumbnailEagerCount`, 6, for the default `Thumbnails` layout (the first
  row of six at the default content width, two rows of three on a phone), `AlbumSection.DefaultEagerCount`, 4, for `Grid` (four 14rem
  columns), and `AlbumSection.DefaultMosaicEagerCount`, 1, for `Mosaic`. A mosaic is a set of CSS columns filled down the page, so document order
  runs down the first column: photos 2 to 4 sit under the first one, and which photos open the other columns depends on their heights
  and the viewport. Only the first photo is surely in the first row; the browser still fetches lazy photos that are already in view.
  An explicit `EagerCount` applies to either layout, counting down the first column of a mosaic.
- `AlbumSection.PrioritizeFirstImage` makes the first photo the page's main image (eager, `fetchpriority="high"`), for an album page
  whose first photo is the largest element on the first screen.

Lower the counts when a hero or text comes before the album, and raise them for a wider content width.

Each page of an album has its own address: page 1 without a page parameter and every other page with one, linked with plain `a href`
links ([GOV.UK pagination](https://design-system.service.gov.uk/components/pagination/),
[Google Search Central: pagination and incremental page loading](https://developers.google.com/search/docs/specialty/ecommerce/pagination-and-incremental-page-loading)).
Give each page its own `PageHead.CanonicalUrl`, and answer a page past the end (`Window.IsOutOfRange`) with 404 Not Found.

```razor
<AlbumSection Title="@view.Title" Slug="@view.Slug" Level="1"
              TitleLanguage="@(view.Culture == PageCulture ? null : Tags.LanguageTagOf(view.Culture).Value)"
              Photos="@Renditions.Gallery(page.PhotoIds)" Window="@page.Window"
              PageHref="@(p => LinkTarget.FromString(p == 1 ? $"/pt/galeria/{view.Slug}" : $"/pt/galeria/{view.Slug}?pagina={p}"))"
              PagerLabel="Páginas do álbum" PreviousText="Anterior" NextText="Próxima" PageLabelFormat="Página {0}">
    <p><a href="@TourHref">Ver a viagem</a></p>
</AlbumSection>
```

`PandaBear.SharedKernel.Galleries` holds the album model behind these components; its `AlbumView.Page` gives the photos and the window
of a page.

### Bands and photos

On the primary colour or a photo, text is the on-primary colour in the light scheme (`color-scheme: light`), whatever the visitor's
scheme: a `Primary` or `Image` band and a `Cover` hero. Inside them headings, links, a section's eyebrow and intro, the hero's
eyebrow and subtitle, a quote and a byline's job title take that colour, the focus ring is the on-primary colour, a primary button
turns to the on-primary colour with primary text, and an outline button to an on-primary border and text. Cards and the contact card
keep the text colour on their own background.

A photo lies behind the content, darkened by the theme's text colour at the computed scrim (see [Scrim](#scrim)), so the text is
readable on any photo the owner uploads, an all-white one included:

- an `Image` band uses `--site-scrim` (normal text reaches 4.5:1);
- a `Cover` hero uses `--site-scrim-large` (its text is large: the `h1`, and the eyebrow and subtitle, which a cover hero sets at
  least 1.5rem; the theme rules keep the `h1` large text). An outline button and a plain link in its actions get a backing of
  `--site-scrim-boost`, so their normal-size text reaches 4.5:1 too. Without an image a cover hero is a band of the primary colour.

`SiteHeader` with `Placement="HeaderPlacement.OverHero"` is a band of the primary colour, its brand, links, menu button and focus ring
the on-primary colour in the light scheme, and from the `60rem` header breakpoint (see [Menu](#menu)) on it lies over the hero: transparent, with no strip. It does so only when a `Cover` hero is the first child of the element right after the header,
as in `<SiteHeader … /><main><Hero Layout="HeroLayout.Cover" … />…</main>`; on any other page, and below the breakpoint, it stays above
the content as its primary band, so it can never cover a page that does not open with a cover hero. The rule is structural (`:has(+ * > .site-hero--cover:first-child)`)
instead of a second parameter on `Hero`, so the two cannot disagree. That hero leaves the header's height free: its top padding is
`--site-header-height` plus the section spacing, and the header over it is exactly that tall. The theme computes the height
(`SiteTheme.HeaderHeight`, see [Header](#header)) from its brand height, which also sizes an `img` in the brand slot
(`--site-brand-height`), so a site never sets a height of its own. Over a photo the header adds a layer of `--site-scrim-boost`, sized to the header itself and fading out below it, so
behind the menu the photo is darkened to at least `--site-scrim` (4.5:1) and fades to `--site-scrim-large` (3:1) where the hero's large
text sits, even when the header wraps to more lines than the room. Over a cover hero without a photo there is no layer: the header
text is on the primary colour.

## Value objects

- `LinkTarget` is every link and image address. `LinkTarget.Create` returns a `Result`, and `LinkTarget.FromString` throws
  `ArgumentException`; there is no implicit conversion from text, so markup writes `Href="@LinkTarget.FromString("/tours")"` for an
  author-controlled literal, and code builds addresses from external data with `Create`. Accepted are:
  - relative addresses (`/tours`, `tours/one`, `?page=2`, `#top`), but not protocol-relative ones (`//host`);
  - absolute `http` and `https` addresses that `PandaBear.BuildingBlocks.WebUrl` accepts: `//`, a host and no user information
    (`https:host`, `https://user:pw@host` and `https://@host` are refused). The target keeps the address exactly as supplied,
    not `WebUrl`'s lower-cased scheme and host;
  - `mailto:` with exactly one address and no query or headers (`?`, `&`, `;`, `,` and `%` are refused);
  - `tel:` with digits and an optional leading `+`.

  Every address is refused when it has white space anywhere (including `\u0085` and no-break space), a control character or a
  backslash, or is longer than `LinkTarget.MaxLength` (2048, the same as `WebUrl.MaxLength`). Root-relative checks reuse
  `PandaBear.InputNormalization.WebAssetUriSanitizer`, which also refuses lone surrogates. Error messages never repeat the input.
- `ResponsiveImage` is the image of `Hero.Image`, `Card.Image` and `GalleryImage.Image`. `ResponsiveImage.Create(src, width,
  height, alt)` is one `img`; the optional `sizes`, `srcSet` and `sources` add responsive candidates:
  - `ImageCandidate.AtWidth(src, 640)` (`640w`) or `ImageCandidate.AtDensity(src, 2m)` (`2x`) is one `srcset` entry. One set uses
    one descriptor kind and never repeats a descriptor; `sizes` is required as soon as a width descriptor is used.
  - `ImageSource.Create("image/avif", candidates)` is one `picture` `source`. The type must be an `image/` media type (it is
    trimmed and lower-cased). List the most efficient format first: the browser takes the first type it supports. With
    sources the component renders `<picture>`, each `source` with its `type`, `srcset` and the shared `sizes`, and the `img`
    last as the fallback.

  Every address (the fallback and each candidate) must be root-relative or `https` (`LinkTarget.IsImageSource`), and a candidate
  address must not start or end with a comma, which would split the `srcset`; `Create` throws `ArgumentException` with
  `ParamName` `src`. `width` and `height` are the fallback's intrinsic size and must be positive
  (`ArgumentOutOfRangeException`), so the browser reserves the space and the layout does not shift.
- `ImageAlternative` is the accessibility decision for an image: `ImageAlternative.Describe("what it shows")` or
  `ImageAlternative.Decorative()` (rendered as `alt=""`). `ResponsiveImage.Create` requires one (`ArgumentNullException`).
- `ContactCard` takes `PandaBear.BuildingBlocks.EmailAddress` and `PhoneNumber`. It checks restored values again, so only digits reach a
  `tel:` or WhatsApp link and no query reaches a `mailto:` link.
- `PageHead` addresses are `System.Uri` and must be absolute `http` or `https`.
- Cultures are `PandaBear.BuildingBlocks.ContentCulture` (`LanguageLink.Culture`, `AlternateLink.Culture`, `PageHead.Locale`), so the
  language-tag rule and canonical casing (`pt-br` becomes `pt-BR`) live in one place. `hreflang` and `lang` use the canonical name;
  `og:locale` writes the language and region with an underscore (`pt_BR`) and drops a script subtag (`zh-Hant-TW` gives `zh_TW`). Build cultures from input with `ContentCulture.Create`; `Restore` skips validation.

### Responsive image example

```csharp
var cover = ResponsiveImage.Create(
    LinkTarget.FromString("/media/atacama-1600.jpg"), 1600, 900, ImageAlternative.Describe("Salt flats at dusk"),
    sizes: "(min-width: 48rem) 50vw, 100vw",
    srcSet: [ImageCandidate.AtWidth(LinkTarget.FromString("/media/atacama-800.jpg"), 800),
             ImageCandidate.AtWidth(LinkTarget.FromString("/media/atacama-1600.jpg"), 1600)],
    sources:
    [
        ImageSource.Create("image/avif", [ImageCandidate.AtWidth(LinkTarget.FromString("/media/atacama-800.avif"), 800),
                                          ImageCandidate.AtWidth(LinkTarget.FromString("/media/atacama-1600.avif"), 1600)]),
        ImageSource.Create("image/webp", [ImageCandidate.AtWidth(LinkTarget.FromString("/media/atacama-800.webp"), 800),
                                          ImageCandidate.AtWidth(LinkTarget.FromString("/media/atacama-1600.webp"), 1600)])
    ]);
```

### Migrating from the earlier image parameters and the old public site

- `Hero` and `Card`: replace `ImageSrc`, `ImageAlt`, `ImageWidth` and `ImageHeight` with
  `Image="@ResponsiveImage.Create(src, width, height, alt)"`. `new GalleryImage(src, alt, width, height, caption)` becomes
  `new GalleryImage(ResponsiveImage.Create(src, width, height, alt), caption)`.
- The ViajantesTurismo public site's `ResponsiveTourImage.razor` maps to a `ResponsiveImage`: its rendition groups become one
  `ImageSource` per content type with an `ImageCandidate.AtWidth` per rendition (AVIF, then WebP, then JPEG, as its
  `ContentTypeSortOrder` did), its `Sizes` parameter becomes `sizes`, and `TourImageRenditions.Fallback` gives the `src`,
  `width` and `height`. `TourImageRenditions` stays in the application: choosing renditions and building media routes is
  application policy. `Loading="eager"` with `FetchPriority="high"` is what `Hero` renders; `TourGallery` maps to `ImageGrid`
  with `PrioritizeFirstImage`, and its `sizes` (`(min-width: 48rem) 50vw, 100vw`) goes into each image.

## Design tokens

Override any token on `:root` in your own stylesheet. Each colour token is defined once with `light-dark(light, dark)`, and `:root`
declares `color-scheme: light dark`, so the browser picks the side that matches the user's colour scheme. Override a colour with
`light-dark()` too, so one declaration sets both schemes:

```css
:root {
    --site-color-primary: light-dark(#0f766e, #5eead4);
    --site-color-primary-hover: light-dark(#115e59, #2dd4bf);
}
```

A plain value (`--site-color-primary: #0f766e;`) applies to both schemes; that is intentional, but check its contrast against the
dark surface too. `light-dark()` needs a 2024 or later browser (Chrome 123, Firefox 120, Safari 17.5).

| Token | Default (light / dark) | Use |
| --- | --- | --- |
| `--site-color-primary` | `#1d4ed8` / `#93c5fd` | Links, primary buttons, current items |
| `--site-color-primary-hover` | `#1e40af` / `#60a5fa` | Primary and outline buttons on hover |
| `--site-color-on-primary` | `#ffffff` / `#111827` | Text on the primary colour |
| `--site-color-surface` | `#ffffff` / `#111827` | Page background |
| `--site-color-surface-alt` | `#f3f4f6` / `#1f2937` | Cards, quotes, contact card |
| `--site-color-text` | `#1f2937` / `#f3f4f6` | Body text |
| `--site-color-muted` | `#4b5563` / `#9ca3af` | Secondary text |
| `--site-color-border` | `#d1d5db` / `#374151` | Borders and rules |
| `--site-color-accent` | `#b45309` / `#fbbf24` | Divider, secondary buttons |
| `--site-color-accent-hover` | `#92400e` / `#f59e0b` | Secondary buttons on hover |
| `--site-color-on-accent` | `#ffffff` / `#111827` | Text on the accent colour |
| `--site-color-focus` | `#1d4ed8` / `#93c5fd` | Focus ring colour |
| `--site-focus-ring` | `3px solid` focus colour | `:focus-visible` outline |
| `--site-font-body` | system UI stack | Body font; no web font is loaded |
| `--site-font-heading` | `--site-font-body` | Heading font |
| `--site-font-size-sm` … `--site-font-size-3xl` | fluid `clamp()` scale | `--site-font-size-sm`, `--site-font-size-base`, `--site-font-size-lg`, `--site-font-size-xl`, `--site-font-size-2xl`, `--site-font-size-3xl` |
| `--site-line-height` | `1.6` | Body line height |
| `--site-space-1` … `--site-space-6` | `0.25rem` to `4rem` | `--site-space-1`, `--site-space-2`, `--site-space-3`, `--site-space-4`, `--site-space-5`, `--site-space-6` |
| `--site-radius` | `0.5rem` | Corner radius |
| `--site-content-width` | `72rem` | Maximum content width |
| `--site-card-min-width` | `16rem` | Smallest card column of `CardGrid` |
| `--site-scrim` | `64%` | Darkening of a photo behind normal on-primary text (image bands, the video play mark); computed by the theme |
| `--site-scrim-large` | `50%` | Darkening of a photo behind large on-primary text (cover hero); computed by the theme |
| `--site-scrim-boost` | `28%` | Second layer that brings `--site-scrim-large` up to `--site-scrim` (the header over a hero, outline buttons in a cover hero); computed by the theme |
| `--site-brand-height` | `2.5rem` | Height of an `img` in the header's brand slot; the theme's `BrandHeight` |
| `--site-header-height` | `4.5rem` | Height of a header over a hero, and the room the hero leaves for it; computed by the theme |
| `--site-floating-link-size` | `3.5rem` | Diameter of `FloatingLink` from the `48rem` breakpoint on, and the room the end of the page keeps for it |
| `--site-floating-link-size-narrow` | `3rem` | Diameter of `FloatingLink` below the `48rem` breakpoint |
| `--site-text-width` | `65ch` | Measure of a section's intro and of paragraphs and lists in a section body, a split's text, a band or the gutter container |
| `--site-thumbnail-min-width` | `6.5rem` | Smallest column of `ImageGrid`'s `Thumbnails` layout |
| `--site-thumbnail-aspect-ratio` | `1` | Aspect ratio every thumbnail is cropped to |
| `--site-video-width` | `40rem` | Largest width of `VideoLink` |

Every default text pair reaches a WCAG contrast ratio of at least 4.5:1 in both schemes (text, muted and primary on surface and
surface-alt, accent on surface, on-primary on primary and on primary-hover, on-accent on accent and on accent-hover, primary-hover on
surface and surface-alt); a test computes the ratios for both sides of every `light-dark()` pair. Buttons change to the hover tokens
on hover, never to a filter, so the hover state keeps the checked contrast. Light
values: text 14.7:1, muted 7.6:1, primary 6.7:1 and accent 5.0:1 on white. The stylesheet respects `prefers-reduced-motion`, shows a
visible `:focus-visible` ring, is mobile first with a `48rem` layout breakpoint and a `60rem` header breakpoint, and loads no fonts, images or other URLs.

## Owner-editable theme

`PandaBear.Blazor.Site.Theming` turns colours, typography and self-hosted fonts that a site owner edits into a second stylesheet
that `site.css` consumes. Without a theme nothing changes: every per-role variable falls back to the value `site.css` used before.

### Model

- `CssColor`: `#rrggbb` or `#rgb` (normalized to lower-case `#rrggbb`), with `RelativeLuminance` and `ContrastRatio(other)`
  (WCAG 2.2 formula, unrounded). `ThemeColor.Create(light, dark)` is one token for both schemes.
- `SiteColors.Create(primary, onPrimary, accent, onAccent, text, muted, surface, surfaceAlt, border, focus, primaryHover?, accentHover?)`.
  A hover colour that is not given is derived: its base mixed with 20% of the text colour per scheme (`IsDerived(role)`).
- `TypographyStyle.Create(fontKey, fallback, size, weight, lineHeight, letterSpacing, transform, italic)` for each `TypographyRole`
  (h1 to h6, body, small, link, button, nav). Every value is optional; an unset value keeps `SiteTheme.Default`, and links, buttons
  and navigation inherit from their surroundings when unset (so a link in small print stays small). `FontSize.Fixed(rem)` or
  `FontSize.Fluid(minRem, maxRem)` (0.5 to 10rem); a fluid size grows linearly from a 20rem (320px) to an 80rem (1280px) viewport as
  `clamp(min, a rem + b vw, max)`. Weight: 100 to 900 in steps of 100. Line height: 0.8 to 3. Letter spacing: -0.1em to 0.5em.
  Transform: none, uppercase or capitalize.
- `ThemeFont.Create(key, family, faces)` with `ThemeFontFace.Create(weight, style, src)`. Fonts are self-hosted uploads: `src` is a
  `LinkTarget` that is root-relative or https and names a `.woff2` file, without `" ' ( ) < >` or a fragment. The family name allows
  letters, digits, single inner spaces, hyphens and underscores (at most 64 characters); the key is a lower-case slug. A role's
  `fallback` (`SansSerif`, `Serif`, `Monospace`) is the system stack written after the uploaded family.
- `SiteTheme.Create(colors, typography, fonts?, allowSmallText, brandHeight, colorLabel?)` enforces every rule;
  `SiteTheme.CreateDraft(...)` only requires unique font keys, existing font references and a positive brand height with at most four
  decimals, so a preview can show a theme that still breaks a rule. `brandHeight` defaults to `SiteTheme.DefaultBrandHeight` (2.5rem).
  `theme.Review(colorLabel?)` returns the errors, the warnings and every `ContrastCheck`. `SiteTheme.Default` is `site.css` as a
  theme. Every value object factory returns a `Result`; errors carry field paths such as `colors.muted`, `typography.h1.size`,
  `typography.body.lineHeight`, `fonts[0].key`, `header.brandHeight`.
- Contrast messages name the colours by their CSS names (`muted on surface-alt is 1.33:1 in the light scheme; …`). A host whose
  editor shows other names passes `colorLabel`, a `Func<ThemeColorRole, string?>` such as `role => role switch { ThemeColorRole.SurfaceAlt
  => "Card background", … }`, to `Review`, `Create`, `ThemeEditorModel.ToTheme` and `ThemePreview.ColorLabel`; the message then reads
  `Muted text on Card background is …`. A role it returns `null` or white space for keeps its CSS name; field paths do not change.

### Scrim

`SiteScrim.For(colors)` computes, from the light colours, the least whole percent of the text colour laid over a photo
(`color-mix(in srgb, var(--site-color-text) p%, transparent)`) at which the on-primary colour reaches a contrast on any photo: `Text`
for 4.5:1, `LargeText` for 3:1, and `Boost`, the opacity of a second layer over `LargeText` that reaches at least `Text`
(`1 - (1 - Text) / (1 - LargeText)`, rounded up). Any photo means the worst one: white for a light on-primary colour, black for a dark
one; a channel is rounded towards that photo, so the browser's rounding never lowers the contrast. With the default colours (white on
`#1f2937`) the values are 64%, 50% and 28%. `SiteScrim.MinimumPercent(foreground, scrim, minimum)` and `SiteScrim.WorstRatio(foreground,
scrim, percent)` expose the computation. When even an opaque scrim does not give 4.5:1 (`IsReachable` is false; the on-primary colour
on the text colour is below 4.5:1), the theme review reports an error and the values are 100, the best a draft can show.

### Header

`SiteTheme.BrandHeight` is the height in rem of the logo in the header's brand slot. `SiteTheme.HeaderHeight` is the height of the
header in one row: its block padding (2 × 1rem, `--site-space-3`) plus the taller of the brand height and a navigation line, which is
the navigation role's largest font size (the body's when that role sets none) times its line height (the body's when unset), plus the
links' block padding (2 × 0.25rem, `--site-space-1`), rounded up to four decimals. With the default theme: 2rem + max(2.5rem,
1.125rem × 1.6 + 0.5rem) = 4.5rem; a 3.5rem logo gives 5.5rem. A test keeps those paddings and `site.css` in step.

### Rules

| Rule | Threshold | Severity | Source |
| --- | --- | --- | --- |
| Text pairs: text, muted, primary and primary-hover on surface and surface-alt; on-primary on primary and primary-hover; on-accent on accent and accent-hover | 4.5:1 in the light and the dark scheme | Error on `colors.{foreground}` | WCAG 2.2 SC 1.4.3 |
| Focus ring on surface and surface-alt | 3:1 in both schemes | Error on `colors.focus` | WCAG 2.2 SC 1.4.11 |
| On-primary over a photo darkened by the text colour: on-primary on text, the opaque scrim | 4.5:1 in the light scheme | Error on `colors.onPrimary` | WCAG 2.2 SC 1.4.3 |
| Page heading (`h1`, smallest value) | large text: at least 1.5rem, or 1.1667rem (14pt) at weight 700 or more, because it sits on the large-text scrim of a cover hero | Error on `typography.h1.size` | WCAG 2.2 SC 1.4.3 |
| Body size (smallest value of a fluid size) | at least 1rem unless `allowSmallText` | Error | readability policy |
| Every other role with a size | at least 0.875rem unless `allowSmallText` | Error | readability policy |
| Body line height | 1.3 to 2 | Error | readability policy |
| Brand height | 1.5rem to 8rem | Error on `header.brandHeight` | layout policy: a smaller logo is hard to read, a taller header takes too much of a cover hero |
| Fluid size | maximum at most 2.5 times the minimum | Error | WCAG 2.2 SC 1.4.4 (zoom must still double the text) |
| Heading order: each level not larger than the level above, minimum and maximum compared | — | Warning on the smaller level's `size` | design guidance |
| Font references and keys | key exists, keys unique | Error, also for drafts | rendering |

Headings use the text colour, which already needs 4.5:1, so the 3:1 large-text allowance of SC 1.4.3 never lowers a check. Borders
are not checked: the components never rely on a border alone to identify a control (SC 1.4.11 applies to the focus indicator here),
and the default `#d1d5db` on white is a decorative 1.5:1. Heading order is a warning, not an error: WCAG conveys structure through
heading markup (SC 1.3.1), not size, and a design can mark a level with weight, case or spacing instead.

### Stylesheet

`SiteThemeStylesheet.Render(theme)` writes `@font-face` rules (`font-display: swap`, key then weight order), then `:root` with the 12
colour tokens as `light-dark(light, dark)`, the three scrim tokens as percentages (`SiteThemeStylesheet.ScrimVariables`), the brand and header heights in rem
(`SiteThemeStylesheet.HeaderVariables`: `--site-brand-height`, `--site-header-height`) and the set
typography values as `--site-{role}-{property}`, where role is `h1` … `h6`,
`body`, `small`, `link`, `button` or `nav`, and property is `font-family`, `font-size`, `font-weight`, `line-height`,
`letter-spacing`, `text-transform` or `font-style` (`SiteThemeStylesheet.RoleVariables`). The output is deterministic; every value
comes from a validated value object, and strings are escaped again. `SiteThemeStylesheet.Hash(theme)` is the first 16 hex digits of
the SHA-256 of the UTF-8 output. A host serves it with immutable caching, after `site.css`:

```csharp
app.MapGet("/theme.css", async (IThemeStore store, HttpContext http) =>
{
    var theme = await store.Current(http.RequestAborted) ?? SiteTheme.Default;
    http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    return Results.Text(SiteThemeStylesheet.Render(theme), "text/css; charset=utf-8");
});
```

```html
<link rel="stylesheet" href="_content/PandaBear.SharedKernel.Blazor.Site/site.css" />
<link rel="stylesheet" href="/theme.css?v=@SiteThemeStylesheet.Hash(theme)" />
```

The package keeps no endpoint code; `IThemeStore` stands for the application's storage. Upload font files with the application's
file intake and store only their addresses in the theme.

### Preview

`ThemePreview` shows a small sample (navigation strip that starts with a brand box of the brand height, labelled `Texts.Brand`, h1 to h3, a paragraph with a link, small print, a primary and a secondary
button, a card) styled by a `style` element scoped to `ScopeId`, then a contrast table for the shown scheme (pair, ratio rounded
down, minimum, `Pass` or `Fail` in text) and the errors and warnings by field path. The sample's headings are `div` elements styled
with the h1 to h3 roles (classes `site-theme-preview__h1` to `__h3`), not heading elements, so the page's outline and heading
navigation keep only the host's own headings. The sample is `inert`, so its links stay out of the tab order; the contrast table and
the issue list are outside it. It needs no script and re-renders on every parameter change:

```razor
<ThemePreview Theme="_draft" Errors="_errors" Scheme="_dark ? ColorScheme.Dark : ColorScheme.Light" Narrow="_narrow" ScopeId="brand-preview" />

@code {
    private readonly ThemeEditorModel _model = ThemeEditorModel.FromTheme(SiteTheme.Default);
    private SiteTheme _draft = SiteTheme.Default;
    private IReadOnlyDictionary<string, string[]>? _errors;
    private bool _dark, _narrow;

    private void OnFieldChanged()
    {
        var draft = _model.ToDraft();
        _errors = draft.IsSuccess ? null : draft.WithoutValue().ValidationErrors();
        if (draft.IsSuccess) { _draft = draft.Value; }
    }
}
```

The inline `style` element needs a nonce: pass the response's CSP nonce in `Nonce` and allow `style-src 'self' 'nonce-…'` (see
`AddCspNonce` in PandaBear.SharedKernel.AspNetCore); the preview writes no style attributes. On an interactive page, use the nonce of
the document's response: read it in the static `App`, pass it to the interactive `Routes` as a parameter and cascade it, because the
circuit's own scope has a different one and interactive navigation sends no new response. The public site loads `/theme.css` from a
URL instead.

`ColorLabel` names the colours of the contrast table's pairs and of the contrast messages by the host's labels (`Body text on Page
background`); the swatches keep the CSS names in `data-foreground` and `data-background`. Give `ToTheme` the same labels, so a save
error reads like the preview's issue and, passed back in `Errors`, is listed once.

`ThemeEditorModel` is the form model: string colour fields (`colors.primary.light`), nullable numbers for sizes, weight and spacing,
fonts with faces, and `BrandHeight` (empty means the default). `ToDraft()` and `ToTheme(colorLabel?)` return every error at once on its field path. Both hover fields empty
means derived.

### Mapping the earlier Branding settings

`PandaBear.Branding.BrandingSettings` (the ViajantesTurismo Branding context) maps as follows: `PrimaryColor` to `primary`,
`AccentColor` to `accent`, `BackgroundColor` to `surface`, `TextColor` to `text`, each as the light value with a dark value chosen by
the owner (or `SiteTheme.Default`'s); on-primary, on-accent, muted, surface-alt, border and focus come from `SiteTheme.Default` until
edited, and `Create` reports any pair that the brand colours break. `HeadingFontFamily` and `BodyFontFamily` become the font keys of
h1 to h6 and body; a system family (`system-ui`, `Arial`, `Verdana`, `Georgia`) maps to no key with the `SansSerif` or `Serif`
fallback, and `Inter` maps to an uploaded `ThemeFont`. `BrandName` and `LogoUri` are not part of the theme.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Blazor.Site`. C# namespace: `PandaBear.Blazor.Site`. Assembly/project identity: `SharedKernel.Blazor.Site`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
