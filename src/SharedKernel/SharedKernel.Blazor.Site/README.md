# SharedKernel.Blazor.Site

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Brand-neutral, accessible Razor components for a public website, plus one stylesheet of design tokens. The components use semantic
landmarks, need no JavaScript and no interactive render mode, and work with static server-side rendering. Every component except
`PageHead` and `JsonLd` takes a `class` and passes any other unmatched attribute to its root element. An unmatched name that starts with an
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
@using SharedKernel.Blazor.Site

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
| `SiteHeader` | `header` with a brand slot, a labelled `nav` (`Label` is required when there are items), and an actions slot (`site-header__actions`). An item has either an `Href` or one level of children; an item with children is a native `details` that starts closed and shows a chevron that turns when it opens. The item that matches `CurrentPath` gets `aria-current="page"`, and the summary of its group gets `aria-current="true"` and the current item's style, so the group stays closed and does not cover the page. Below the `48rem` breakpoint an open group stacks the navigation on a row of its own, the group's links under its summary; from the breakpoint on they drop down over the page. `MenuLabel` collapses the navigation and the actions behind a menu button below the breakpoint, and `Placement` lays the header over a cover hero; see [Menu](#menu) and [Bands and photos](#bands-and-photos). The brand slot lays its link out as a box around the logo (`display: flex`), so the link's focus ring surrounds the logo; a link of text, such as the site's name without a logo, takes the `Brand` typography role (a heading face, bold, no underline; see [Model](#model)). A `LanguageSwitcher` in the actions takes the navigation role's type (`--site-nav-*`), so its links sit on the line of the navigation links. |
| `SiteFooter` | `footer` with a content slot and an optional `Legal` line, its lines `--site-space-2` apart (a `p` directly in either slot has no margin, and an `address` is upright). `Tone` `Default` (the page background in the content width under a border, muted text) or `Primary` (`site-footer--primary`: a full-width band of the primary colour in the light scheme, on-primary text and links, an on-primary focus ring, and buttons inverted as in a `Primary` band, the content in the content width); `TextAlignment` `Center` (`site-footer--text-center`) centres its lines. Below the `48rem` breakpoint its links, except a `FloatingLink`, are at least 2.75rem (44px) tall tap targets. |
| `SeparatedList`, `SeparatedListItem` | A `ul role="list"` (`site-separated-list`; the role keeps the list in Safari without bullets) of `li` items (`site-separated-list__item`) on one line, such as a footer's contact details or legal links, joined by a bar the stylesheet draws with empty alternative text (`content: "\|" / ""`), so assistive technology reads a list and never the bar. Each item is kept together (`inline-block`), so "Phone: +55 11 99999-0000" never breaks between its label and its number, and breaks inside only when it is wider than the whole line. An item outside a list throws `InvalidOperationException`. Put a list of links in your own labelled `nav`. |
| `Hero` | `Layout` `Split` (the default: text and image side by side from the breakpoint on) or `Cover` (the text centred over the image; see [Bands and photos](#bands-and-photos)). A cover's `CoverHeight` is `Page` (the default: at least 20rem below the breakpoint, 34.375rem from it on) or `Landing` (`site-hero--landing`: at least 20rem below the breakpoint, from it on 85% of the small viewport height between 34.375rem and 52rem); content can make either taller, and `Landing` on a split hero throws `ArgumentException`. Only a cover with an `Image` keeps that minimum height, which exists to show the photo; without one the primary band is as tall as its content and padding, so a not-found or error page gets a compact band instead of an empty block. The page's one `h1`, an optional `Eyebrow` line above it and `Subtitle` line under it, an image loaded eagerly with `fetchpriority="high"`, and an actions slot. With an eyebrow or a subtitle the heading is an `hgroup` (`site-hero__heading`): the eyebrow `p` (`site-hero__eyebrow`), the `h1` (`site-hero__title`), then the subtitle `p` (`site-hero__subtitle`), so reading order matches visual order and neither line enters the outline; with neither it is the `h1` alone. `TitleScale` `Large` (`HeroTitleScale`; `site-hero--title-large`) makes the title the level 1 heading's size, the theme's included, times `--site-hero-title-scale-large` (1.25), in any layout, such as for a landing page; a browser test measures the ratio, and an undefined scale throws `ArgumentOutOfRangeException`. The actions (`site-hero__actions`) keep `--site-space-4` (1.5rem) from the title or the subtitle above them in every layout; a browser test measures it. |
| `Section` | `section` with a heading (`Level`, default 2), a decorative divider and an intro. An optional `Eyebrow`, a short label such as a tour's category, goes above the heading: the heading is then an `hgroup` (`site-section__heading`) of the eyebrow `p` (`site-section__eyebrow`) and the heading (`site-section__title`), so the eyebrow is read first and stays out of the outline and the section's name; the divider and intro follow the `hgroup`. Without an eyebrow (null or blank) the heading stands alone. The eyebrow is small, bold, letter-spaced text in the primary colour, and the on-primary colour in a `Primary` or `Image` band. Set `HeadingId` to give the heading that id and label the section by it (`aria-labelledby`), which makes it a named region; without it no id is generated, so prerendered and interactive markup match. `Media` makes its content a `Split`: the heading, intro and body in `site-split__main`, then the media in `site-split__media`. `TitleLanguage`, a BCP 47 tag such as `en`, marks the heading alone with `lang` when the title is in another language than the page, so the intro and body keep the page's language (WCAG 3.1.2). `TextAlignment` aligns the heading, divider, intro and body text: `null` (the default) inherits the nearest section or band that sets one, or the start; `Center` (`site-section--text-center`) centres them, the divider, paragraphs, a byline, a video link, a default `ActionRow` and an image narrower than its column, keeps a list a centred box whose items stay at the start, and drops a quote's start rule; `Start` (`site-section--text-start`) returns all of that to the start inside a centred ancestor. The alignment classes set inherited private custom properties, so the nearest one wins at any depth. The intro and the paragraphs and lists directly in the body keep a readable measure of `--site-text-width` (65ch) instead of running the content width. The measure and the centring margins are in `:where(...)` rules without specificity, so any rule of yours, even on an element, overrides them. A section inside a section, such as a day of an itinerary, takes the full width of the outer body without vertical padding and is spaced from what comes before it by `--site-space-5`, so the spacing of nested sections does not add up. An undefined alignment throws `ArgumentOutOfRangeException`. |
| `Split` | Text (`ChildContent`) beside media (`Media`, required: `ArgumentNullException` without it), without a heading of its own: a plain `div`, never a landmark, so a heading-less band can put text and photos side by side. The text is in `site-split__main` and the media in `site-split__media`, two equal columns from the `48rem` breakpoint on and the media after the text below it, so reading order matches visual order; the media fills its column. `MediaSide` `End` (the default) puts the media column on the inline-end side; `Start` (`site-split--media-start`) puts it on the inline-start side from the breakpoint on, by grid order only, so the text stays first in reading and focus order and below the breakpoint the media still follows the text; alternate it to zigzag a day-by-day itinerary. `Alignment` `Start` (the default) starts both columns at the top, so a photo stays beside the heading and first paragraph it illustrates instead of floating halfway down a long text; `Center` (`site-split--align-center`) centres the columns on each other, for a short tagline beside a photo. Start is the default because the usual split is text-heavy: a tour description or an itinerary day beside one or a few photos. An undefined side or alignment throws `ArgumentOutOfRangeException`. `Section.Media` uses it with the default alignment, so both are one layout. |
| `Band` | A full-width `div` (never a landmark) around content of the content width: a `Section`, or content without a heading such as a tagline and a `Byline`. `Tone` is `Default`, `Muted` (surface-alt), `Primary` (primary background, on-primary text) or `Image` (a lazy photo darkened by the normal-text scrim, on-primary text), which requires `Image`; an `Image` on another tone throws `ArgumentException`. A section inside a band loses its own vertical padding and keeps its semantics. `TextAlignment` (`null` inherits, `Center` adds `site-band--text-center`, `Start` adds `site-band--text-start`) aligns the band's text and a section in it that sets none, as `Section.TextAlignment` does; paragraphs directly in the band keep the `--site-text-width` measure. |
| `Byline` | A person: a lazy round photo (`Photo`, usually decorative), the `Name` and the `JobTitle`, each a `p`. |
| `VideoLink` | A link to a video on its own site instead of an embedded player: a lazy, decorative and self-hosted `Poster` (a described or remote poster throws `ArgumentException`), a play mark on the normal-text scrim, the visible `Title`, and a visually hidden `SiteHint` such as "(opens YouTube)"; the link's name is the title then the hint. `Href` must be an address that `WebUrl.CreateHttps` accepts (absolute `https`); the link carries `rel="noreferrer"`. No frame, no script, and no request to the video's site before the visitor's click. The poster is cropped to 16:9 (`object-fit: cover`), which cuts away the black bars of a 4:3 video thumbnail, and the link is at most `--site-video-width` (40rem) wide, so a 480 by 360 thumbnail is not blown up to the content width; in a centred section it is centred. |
| `FloatingLink` | A round link fixed in the bottom corner, such as a chat link: the icon in `ChildContent` is hidden from assistive technology and the required `Label` names the link in visually hidden text. While it is on the page, focused elements scroll clear of it (`scroll-padding`) and the footer, or the body without a footer, ends with room for it, so it never covers the end of the page. Its hover grows it with a transition that `prefers-reduced-motion` turns off. Below the `48rem` breakpoint, where it lies over the text, it is `--site-floating-link-size-narrow` (3rem, still above a 44px tap target) at `--site-space-2` from the corner, so it reaches 40px into the text column instead of 56px; no side gutter is reserved, because a gutter would narrow every line of a phone page for a button that covers a line at a time while the visitor scrolls. |
| `ButtonLink` | A link styled as a button (`Primary`, `Secondary`, `Outline`), `Size` `Regular` (the default) or `Compact` (`site-button--compact`); see [Shape](#shape) for its computed padding. `NewTab` adds `target="_blank"`, `rel="noopener noreferrer"` and a visually hidden `NewTabHint` after a space, so the link's name reads "Partner (opens in a new tab)" and not "Partner(opens in a new tab)". |
| `ActionRow` | A plain `div` (`site-action-row`, never a landmark) around actions such as `ButtonLink`s and links, in reading order, with a gap. `Justification` places them from the `48rem` breakpoint on in one wrapping row: `Start` (the default) together where reading starts, in line with the text above, so they read as one group, and in the middle inside a section or band with centred text; `Center` (`site-action-row--center`) together in the middle; `End` (`site-action-row--end`) together at the end; `Between` (`site-action-row--between`) spread across the width, for two actions on opposite sides such as previous and next. Below the breakpoint they are stacked at the start (in the middle under centred text), in the middle (`Center` and `Between`) or at the end. An undefined justification throws `ArgumentOutOfRangeException`. A plain link in it keeps the link style in the primary colour. |
| `Card`, `CardGrid` | `article` with an optional lazy image, a heading (`HeadingLevel`, default 3) whose link covers the card, text and actions; an auto-fill grid whose columns are at least `--site-card-min-width` wide and that keeps empty columns, so a lone card has the width of one column instead of the whole row. Links, buttons, form controls, `summary` and `[tabindex]` elements in the body and the actions are raised above the covering link, so they stay clickable; any other interactive element you put in the body needs `position: relative; z-index: 1`. The focus ring is drawn on the card while a control in it has keyboard focus (`:has(:focus-visible)`). `TitleLanguage` marks the card's heading alone with `lang`, as on `Section`. `EagerImage` loads the image eagerly, for a card on the first screen. Typed options set its look, the eyebrow, the more look and the grid's columns; see [Cards](#cards). |
| `StatusBadge` | A short status after the GOV.UK tag: `<strong class="site-status site-status--{tone}">` with the `Text`, a `Tone` (`StatusTone`: `Neutral`, the default, `Info`, `Success`, `Warning`, `Danger`) and an optional visually hidden `Label` prefix; see [Offerings](#offerings). |
| `PriceList` | An offering's prices after the GOV.UK summary list: a `dl` of `PriceItem` rows (label, amount in `<data value>`, optional note) and optional `Conditions`; `HeadingId` makes it a group named by that heading; see [Offerings](#offerings). |
| `FromPrice` | A line such as "a partir de 1.234,50 €": `<p class="site-from-price">` of the `Format` split at its `{0}`, the amount in `<data value>`; see [Offerings](#offerings). |
| `StepList` | Numbered steps, such as the days of an itinerary: `<ol role="list" class="site-step-list">` of `StepItem`s, each an `hgroup` of its number from the position (`NumberFormat`) and its title, inline facts, note and paragraphs, beside its image in a zigzag `Split`; see [Offerings](#offerings). |
| `TextList` | A plain `ul`, or an `ol` when `Ordered`, of lines (`Items`), such as what a tour includes; nothing when empty. |
| `FactsTable` | `table` with a required `caption` and `th scope="row"` headers. |
| `FactGrid` | A `dl` (`site-fact-grid`) of `FactCard`s, such as a tour's basic information: each card is a `div` (`site-fact-grid__item`) of the `dt` label (`site-fact-grid__label`) and one `dd` per value (`site-fact-grid__value`), for example "8 days" and "7 nights". A value is a `FactValue`: its `Text` and an optional `Link` (`LinkTarget`, so a relative, `http`/`https`, `mailto:` or `tel:` address), which makes the text a plain `a` in the same tab, for example `new("info@example.org", LinkTarget.FromString("mailto:info@example.org"))`. A value wraps between words; in a word that holds an `@`, such as an e-mail address, the component writes a `wbr` before the `@` and every dot, so the address wraps between its parts (`info` / `.desk` / `@example` / `.org`) and never inside one; a dot that ends the word or comes before closing punctuation only (`org.`, `org.)`) gets none, and other words, such as `8.5`, keep their dots unbroken. Chromium computes a link's name from its content with a space at every `wbr`, so a linked value that gets breaks carries its text as `aria-label`: the link's name and `textContent` stay the address. Only a part wider than its card breaks inside, so nothing scrolls sideways. A card's optional `Icon` is rendered first inside the `dt`, in a `span` (`site-fact-grid__icon`) with `aria-hidden="true"`, because a `div` in a `dl` may hold only `dt` and `dd` elements; the label alone names the term. The cards are centred and bordered like table cells, in wrapping rows (flex) that share the full width of the container. Below the `48rem` breakpoint as many cards at least 11rem wide fit a row as the width allows; from it on the row length is computed from the number of cards for balanced rows, as the class `site-fact-grid--columns-1` to `site-fact-grid--columns-4` (no inline style): as many as cards up to four, otherwise `ceil(n / ceil(n / 4))`, so 5 and 6 cards make rows of 3, 7 and 8 rows of 4, and 9 rows of 3. The cards of a shorter last row grow to fill it, so 7 cards are a row of 4 and a row of 3 wider cards, and a lone last card on a phone takes the whole row: no row leaves a notch. An empty grid has no column class; the label is bold and, like the icon, in the primary colour (the on-primary colour in a `Primary` or `Image` band). An `svg` icon without a `fill` attribute is filled with the colour; one with `fill="none"` keeps its strokes, so draw them with `currentColor`. `Layout` `Inline` (`FactGridLayout`; `site-fact-grid--inline`, no column class) renders the same list as one compact row for a card, such as days, distance and difficulty: no borders, each fact its icon and label on one line above its value, at the start (in the middle under centred text), wrapping only when the card is too narrow (a browser test measures one row of three short facts in a card of three to a row at 1280px and no sideways scroll at 320px). A null card, a blank label, no values, a null value or a value with blank text throws `ArgumentException`, and an undefined layout `ArgumentOutOfRangeException`. |
| `Disclosure` | Native `details` and `summary`; a tab substitute without script. A disclosure after other content, such as a `FactGrid`, keeps `--site-space-4` from it, and stacked disclosures keep `--site-space-2` between them. The summary is rounded like its box, so its focus ring follows the box's corners. |
| `Quote` | `figure` with a `blockquote` and a `cite` caption. In a `Primary` or `Image` band it drops its background and takes the on-primary colour, its rule included. |
| `ImageGrid` | `figure` per `GalleryImage` (a `ResponsiveImage` and an optional caption), lazy loading except the first `EagerCount`; `PrioritizeFirstImage` loads the first one eagerly with `fetchpriority="high"`. `Layout` `Grid` (the default) is rows of equal columns; `Mosaic` (`site-image-grid--mosaic`) is a collage of mixed aspect ratios: columns filled from top to bottom in document order, each image at its own aspect ratio, never cropped. In both, columns are at least 14rem wide and share the full width of the container, as many as there are images while they fit, so one image fills its container, in a section or in a `Split`'s media column. An image is at most 40rem or 80% of the viewport tall; a taller one is shown whole within that height (`object-fit: contain`), at the start of its box, in line with the text, or in the middle in a section or band with centred text. `Thumbnails` (`site-image-grid--thumbnails`) crops every image to one aspect ratio, `--site-thumbnail-aspect-ratio` (square by default, which crops portrait and landscape photos alike), in columns at least `--site-thumbnail-min-width` (6.5rem) and 15% of the width, so a phone shows two or three to a row and a desktop at most six; empty columns are kept, so a short last row keeps the thumbnails' size. Thumbnails show part of each photo: use them where the visitor browses many photos, and `Grid` or `Mosaic` where each photo must be seen whole. A `GalleryImage` with an `Href` (`new GalleryImage(image) { Href = … }`, for example its full-size file) is wrapped in a plain `a href` (`site-image-grid__link`), so it works in static rendering without script. The link is a block tile, rounded like the image, so its focus ring surrounds the tile, and the caption stays outside it. The link is named by the image's alternative text, so a linked image must be described: a linked decorative image throws `ArgumentException`. A `GalleryImage` with a `Credit` (`ImageCreditLine`) shows it in the `figcaption` after the caption; see [Image credits](#image-credits). `Sizes` replaces the `sizes` of every image with width descriptors; left unset, `Thumbnails` writes `ImageGrid.ThumbnailSizes` (`(min-width: 74rem) 12rem, (min-width: 48rem) 16vw, (min-width: 30rem) 23vw, (min-width: 22.5rem) 31vw, 45vw`), which follows the two to six columns at the default tokens and is at least the tile width and at most about one and a half times it from 320px to 1600px, so the browser fetches a small rendition for a small tile, and the other layouts keep each image's own `sizes`. A blank or multi-line `Sizes` throws `ArgumentException`. |
| `ImageCreditCaption` | The short credit under an image: `<small class="site-image-credit">` of one `Credit` (an `ImageCreditLine`): its `Text` (linked to `CreditsHref`) and `LicenseText` (linked to `LicenseHref` with `rel="license"`); `GalleryImage.Credit` renders one in the image's `figcaption`; see [Image credits](#image-credits). |
| `CreditsList` | A `ul role="list"` (`site-credits-list`) of `CreditItem`s: a lazy thumbnail beside the title linked to the source, the author linked to their page, the licence linked to its deed and the changes; see [Image credits](#image-credits). |
| `AlbumIndex` | A `CardGrid` (`site-album-index`) of `Card`s, one per `AlbumSummary`: the title as the heading linked to the album's page, a cover (the first `EagerCount`, default 4, eager), the photo count as the application words it (`site-album-index__count`), and `lang` on the title alone when it is in another language; see [Photo albums](#photo-albums). It passes `CardStyle`, `ImageAspect`, `ImageShadow`, `TextAlignment`, `MoreText`, `MoreVariant` and `MoreSize` to every card and `MinColumnWidth`, `MaxColumnWidth`, `Gap` and `ItemAlignment` to the grid. Nothing without albums. |
| `AlbumSection` | One page of an album: a `Section` whose heading id is `album-{slug}`, an `ImageGrid` of the page's photos in the `Layout` `Grid` by default (opt into `Thumbnails`), with its `Sizes` and each photo's `Href` passed through (the first `EagerCount` eager: by default 4 in a grid, 6 as thumbnails, 1 in a mosaic), optional content, then the `Pager`; see [Photo albums](#photo-albums). |
| `ContactCard` | `address` with postal lines and `tel:`, `mailto:` and `https://wa.me/` links; no `address` when there are no lines and no channels. `PhoneText` shows the number as written (`+55 (11) 99999-0000`) while the link uses the digits; it must be the same number as `Phone`. The WhatsApp link shows `WhatsAppLabel`. |
| `LanguageSwitcher` | Labelled `nav` of links with `hreflang`, `lang` and `aria-current="true"` on the current language, with the control radius; below the `48rem` breakpoint each link is at least 2.75rem (44px) wide and tall. |
| `Pager` | A `nav` named by `Label` (`site-pager`) with plain links for a list paged through the URL, after the GOV.UK pagination component: previous (`rel="prev"`), the numbered pages and next (`rel="next"`); see [Pager](#pager). Nothing when there is one page. |
| `JsonLd` | Structured data: `<script type="application/ld+json">` of a `JsonNode` (`Data`, an object or an array), escaped with `JavaScriptEncoder.Default` so the data cannot close the script; see [Offerings](#offerings). |
| `PageHead` | Title, description, canonical link, OpenGraph basics and `hreflang` alternates including `x-default`; each optional tag is left out when its parameter is unset. `Robots` writes `<meta name="robots">` (`noindex`, `noindex, nofollow`). An error or not-found page sets `Robots="noindex"` and no `CanonicalUrl`, `Alternates` or `DefaultAlternateUrl`, so it has no canonical, `og:url` or `hreflang` link. |

Content without a heading, such as a page's opening text or a contact block, goes in `<div class="site-container">`: the
`site-container` class gives it the content width and side gutter of `Section`, `Hero`, the header and the footer, so it lines up
with them, without a section's vertical padding. Space it from its neighbours yourself.

`SiteHeader.CurrentPath` matching compares paths only: the query and fragment are ignored, a missing leading slash is added
(`Navigation.ToBaseRelativePath(...)` returns `tours`, which matches `/tours`), a trailing slash is ignored, and the comparison is
ordinal and ignores case. The empty path matches `/`. An item with an absolute address never matches.

Texts that people read (labels, the menu button, the new-tab and video hints, the WhatsApp link text) are parameters so the caller can
localize them.

### Cards

A card's look comes from typed parameters, never from a stylesheet of the site:

- `CardStyle`: `Boxed` (the default: the alternative background, a border and `--site-radius`) or `Plain` (`site-card--plain`: no
  background, no border, the colour of its surroundings, so a plain card in a `Primary` band takes the on-primary colour, and the image
  with `--site-radius` corners of its own). It is not named `Style`: Blazor matches parameter names regardless of case, so a `Style`
  parameter would take the HTML `style` attribute, which instead reaches the `article` like any other attribute.
- `ImageAspect`: `FourByThree` (the default), `SixteenByNine` (`site-card--aspect-16x9`), `Square` (`site-card--aspect-1x1`) or
  `Natural` (`site-card--aspect-natural`: the image's own ratio, never cropped).
- `ImageShadow`: `None` (the default) or `Offset` (`site-card--image-shadow`): a hard block of `--site-color-border` offset
  `--site-space-2` towards the end and the bottom (the left in a right-to-left page, `:dir(rtl)`), decorative and without a contrast need. It fits the design system as a token-built
  decoration; a boxed card refuses it (`ArgumentException`), because its box would clip it.
- `TextAlignment` (`SiteAlignment`): `Start` (the default) or `Center` (`site-card--text-center`), which centres the text, the heading,
  a byline and the action row. A card does not inherit the alignment of a section or band around it: it resets the private alignment
  properties those set, so a card in a centred band stays at the start unless its own alignment centres it (a browser test measures it).
- `Eyebrow`: a short label above the title; the heading is then an `hgroup` (`site-card__heading`) of the eyebrow `p`
  (`site-card__eyebrow`, small, bold, letter-spaced, in the primary colour) and the heading, as on `Section`.
- `MoreText`, `MoreVariant` (default `Outline`) and `MoreSize` (default `Regular`): the more look of a card whose whole surface is its
  link, such as "Learn more". It is a `span` with exactly the classes of a `ButtonLink` of that variant and size, plus
  `site-card__more`, and `aria-hidden="true"`, because the covering title link is the one control. A shared internal style gives both
  their classes, so the more look and a `ButtonLink` of the same variant, size and text are the same size to the pixel (a browser test
  measures them). It lets clicks through to the title link (`pointer-events: none`) and takes the button's hover look while the card is
  hovered, the inverted hover look on the primary colour (see [Bands and photos](#bands-and-photos)). It needs `Href`
  (`ArgumentException`).
- The more look and the `Actions` are one `ActionRow` (`site-card__actions`) at the bottom of the card, so the actions of a row of
  cards line up whatever the length of their text. Its `Justification` is `Center` for a centred card and `Start` otherwise, so it is
  stacked below the `48rem` breakpoint and one row from it on, like any action row. Only the actions take the pointer; a click between them reaches the title link.

The card title has a typography role of its own (`TypographyRole.CardTitle`, see [Model](#model)), so a section heading role in upper
case or a large size does not reach the cards. The card never widens its grid column (`min-width: 0`), and a word longer than the card,
such as a long name in capitals, breaks inside it (`overflow-wrap: break-word`). An undefined option throws `ArgumentOutOfRangeException`.

`CardGrid` takes `MinColumnWidth` (`CardColumnWidth.Narrow` 16rem, the default; `Medium` 22rem; `Wide` 32rem), which sets the number
of columns: as many columns at least that wide as fit, sharing the row. `MaxColumnWidth` (`null`, the default: each card fills its
column and empty columns are kept, so a lone card has the width of one column) caps each card in its column and drops empty columns,
without changing the number of columns; it must be at least the minimum in rem (`ArgumentException`). `ItemAlignment` (`SiteAlignment`)
places a capped card at the start of its column (the default) or in its middle (`site-card-grid--items-center`). `Gap` (`SiteSpace`, a
step of the spacing scale; default `Space4`) spaces the cards. At the default 72rem content width the minimum gives four (`Narrow`),
three (`Medium`) or two (`Wide`) columns; a browser test measures every combination.
They are modifier classes (`site-card-grid--min-medium`, `site-card-grid--max-wide`, `site-card-grid--gap-6`) that set the
`--site-card-min-width`, `--site-card-max-width` and `--site-card-gap` tokens, so no style attribute is written and a strict
Content-Security-Policy holds.

```razor
<CardGrid MinColumnWidth="CardColumnWidth.Medium" MaxColumnWidth="CardColumnWidth.Wide" ItemAlignment="SiteAlignment.Center" Gap="SiteSpace.Space6">
    <Card Title="Alentejo" Href="@LinkTarget.FromString("/tours/alentejo")" Image="_cover" CardStyle="CardStyle.Plain"
          ImageAspect="CardImageAspect.SixteenByNine" ImageShadow="CardImageShadow.Offset" TextAlignment="SiteAlignment.Center"
          MoreText="Learn more">
        <p>Eight days by bike.</p>
        <Actions><ButtonLink Href="_signUp" Variant="ButtonVariant.Outline" NewTab="true">Sign up</ButtonLink></Actions>
    </Card>
</CardGrid>
```

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
and the ellipsis is muted; each link is at least 2.75rem (44px) wide and tall and has the control radius.

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
  text, the pager and your own content keep the page's language (WCAG 3.1.2, language of parts). The tag is yours to choose:
  a content culture such as `pt` may declare `pt-BR` (`CultureLanguageTags.LanguageTagOf` in BuildingBlocks maps one to the other).
- Set `GalleryImage.TextLanguage` when a photo's alternative and caption use a different language from the page. The image, any
  named photo link, and the caption text get that tag. Other photos, credit text and pager labels keep the surrounding language.
  Leave the property unset for photos whose text uses the page language. [W3C guidance](https://www.w3.org/WAI/WCAG22/Understanding/language-of-parts.html)
- `AlbumSection` renders one page of one album: a `Section` named by its heading, whose id is `album-` plus the slug, the same on every
  page and render, so `#album-patagonia` can be linked to; the page's photos in an `ImageGrid`; optional `ChildContent`, such as a link
  to the related tour; then the `Pager` (see [Pager](#pager)) with your `PagerLabel`, `PreviousText`, `NextText`, `PageLabelFormat` and `SurroundingPages`.
  Use `Level="1"` when the album is the page.
- Thumbnails crop the photos and the page has no script, so link each photo to its whole image: give its `GalleryImage` an `Href`, such as
  the largest rendition, and an alternative text, which names the link. `AlbumSection.Sizes` overrides the thumbnail `sizes` when the
  album is narrower than the content width or the thumbnail tokens are changed.

Images visible on the first screen load eagerly and the rest lazily, each with its width and height so the layout does not shift
([web.dev: browser-level image lazy loading](https://web.dev/articles/lazy-loading-images)). Both components count eager images in
document order:

- `AlbumIndex.EagerCount` defaults to `AlbumIndex.DefaultEagerCount`, 4: the first row of covers at the default content width, where
  72rem holds four 16rem card columns with their gaps.
- `AlbumSection.Layout` defaults to `Grid`, each photo whole. Set `Thumbnails` for an album of many photos: in whole-photo columns at
  least 14rem wide a page of 24 photos is one photo per row and about 14,000px tall on a phone, as thumbnails three to a row.
- `AlbumSection.EagerCount` left unset picks `AlbumSection.DefaultEagerCount`, 4, for the default `Grid` layout (four 14rem columns),
  `AlbumSection.DefaultThumbnailEagerCount`, 6, for `Thumbnails` (the first row of six at the default content width, two rows of three on
  a phone), and `AlbumSection.DefaultMosaicEagerCount`, 1, for `Mosaic`. A mosaic is a set of CSS columns filled down the page, so document order
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

### Offerings

Display atoms for a page that offers something, such as a tour: they take display records and text only, so the model, its
formatting and every word stay in the application.

`StatusBadge` shows a status, such as "Open" or "Sold out", after the
[GOV.UK Design System tag](https://design-system.service.gov.uk/components/tag/):

- a `strong` element (`site-status site-status--{tone}`), never a link or a control, with bold small text on the tone's background;
- `Tone` picks the colour pair; the badge's `Text` always says the status, so the colour is never the only cue (WCAG 1.4.1), and a
  transparent outline draws its edge in forced colours;
- `Label`, such as "Status:", is a visually hidden prefix followed by a space, for a badge whose meaning the page does not otherwise
  give; null or blank renders none;
- each tone is a pair of tokens, `--site-color-status-{tone}` behind `--site-color-on-status-{tone}`, at least 4.5:1 in both schemes
  (a test computes every pair). The badge carries its own background everywhere, so on the primary colour of a band, a cover hero
  or a footer it keeps the light pair of the page; a browser test measures it in each of them.

A blank text throws `ArgumentException` and an undefined tone `ArgumentOutOfRangeException`.

```razor
<StatusBadge Text="Esgotado" Tone="StatusTone.Danger" Label="Situação:" />
```

`PriceList` shows the prices of an offering after the
[GOV.UK Design System summary list](https://design-system.service.gov.uk/components/summary-list/):

- `Items` are `PriceItem(Label, AmountText, Value, Note)` records: `AmountText` is the amount as the page shows it ("1.234,50 €"),
  formatted by the application, and `Value` (`decimal?`) the number, written with the invariant culture into `<data value="1234.50">`
  so machines read it whatever the page's culture; `null`, for an amount such as "On request", writes the text without `data`;
- the markup is `div.site-price-list` > `dl.site-price-list__items` > a `div.site-price-list__row` per item of the `dt` label
  (`site-price-list__label`), the `dd` amount (`site-price-list__amount`, bold) and an optional `dd` note (`site-price-list__note`,
  muted small text, the on-primary colour on the primary colour of a band); rows sit under a rule, the label at the start and the amount
  at the end of one line, wrapping below each other when the label is long;
- the amount has `white-space: nowrap`: its text holds a normal space before "€", which would otherwise break "1.234,50" from its
  currency at the end of a line (a browser test measures it at 320px);
- `Conditions` (a `RenderFragment`), such as the deposit and the payment terms, follow in `site-price-list__conditions`;
- `HeadingId`, the id of the heading above the prices, makes the root `role="group"` with `aria-labelledby`, so the lines and the
  conditions are one group named "Prices" (a browser test reads Chromium's accessibility tree);
- no items render no `dl`. A null item, a blank label or amount, or an empty or spaced `HeadingId` throws `ArgumentException`.

`FromPrice` is the lowest price in a line of text, such as on a card: `Format` holds exactly one `{0}` ("a partir de {0}"), and the
component writes the text before it, the amount in `<data class="site-from-price__amount" value="...">` (bold, `nowrap`), then the
text after it, in `<p class="site-from-price">`. The text around the placeholder is written as it is, never formatted, so other braces
are plain text. A format without exactly one `{0}`, with the placeholder escaped as in `string.Format` (`{{0}}`), or a blank
`AmountText` throws `ArgumentException`.

```razor
<Section Title="Valores" HeadingId="valores">
    <PriceList HeadingId="valores" Items="_prices">
        <Conditions><p>Reserva com sinal de 30%.</p></Conditions>
    </PriceList>
</Section>
<FromPrice Format="a partir de {0}" AmountText="1.234,50 €" Value="1234.50m" />

@code {
    private readonly PriceItem[] _prices =
    [
        new("Quarto duplo", "1.234,50 €", 1234.50m, "por pessoa"),
        new("Suplemento individual", "300 €", 300m),
    ];
}
```

`StepList` shows numbered steps, such as the days of an itinerary:

- `Steps` are `StepItem(Title, Paragraphs, Note, Image, Facts)` records. A step's number comes from its position, never from the data,
  formatted with the application's `NumberFormat` ("Dia {0}", "Day {0}") and the current culture;
- the markup is `<ol role="list" class="site-step-list">`. The list has no markers, and Safari drops the list semantics of a list
  styled `list-style: none`, so the explicit role keeps it a list of its steps;
- each `li` (`site-step-list__item`) opens with an `hgroup` (`site-step-list__heading`) of the number `p` (`site-step-list__number`,
  small bold text in the primary colour, the on-primary colour in a `Primary` or `Image` band) and the title heading
  (`site-step-list__title`, `HeadingLevel`, default 3), so the number is read first and stays out of the outline; then the `Facts` as an
  inline `FactGrid` (`site-step-list__facts`), the `Note` (`site-step-list__note`), and a `p` per paragraph;
- a step with an `Image` (a `GalleryImage`, with its caption and credit) puts that text beside a one-image `ImageGrid` in a `Split`
  with `Alignment` `Start`, so the photo starts beside the title. With `Alternate` (the default) the image side alternates from image
  to image, end then start, so the photos zigzag, counting only steps with an image; below the `48rem` breakpoint every photo follows
  its text. `EagerCount` (default 0) loads the first images eagerly, in document order;
- `HeadingIdFormat`, such as "dia-{0}", gives each title an id from its position so `#dia-2` links to a step; `null`, the default,
  writes none, because two lists on a page would repeat a fixed default.

A missing step, a blank title or paragraph, a missing paragraph or fact list or fact, a number format that reads the same for positions 1 and 2 (no `{0}`, or an escaped `{{0}}`), a heading id
format that does not give positions 1 and 2 different results (so an escaped `{{0}}` is refused) or that gives white space, or a negative `EagerCount` throws `ArgumentException` (`ArgumentOutOfRangeException`
for the count and a heading level outside 1 to 6).

`TextList` is a plain list of lines, such as what a tour includes or the practical information: a `ul`, or an `ol` when `Ordered`, with
the class `site-text-list`, one `li` per line and the browser's bullets or numbers. An empty list renders nothing; a null or blank line
throws `ArgumentException`.

```razor
<Section Title="Roteiro" HeadingId="roteiro">
    <StepList Steps="_days" NumberFormat="Dia {0}" HeadingIdFormat="dia-{0}" EagerCount="1" />
</Section>
<Disclosure Summary="Incluído"><TextList Items="_included" /></Disclosure>
```

`JsonLd` writes structured data, such as a schema.org `TouristTrip`, as `<script type="application/ld+json">`. `Data` is a `JsonNode`
(a `JsonObject`, or a `JsonArray` of objects; a bare value throws `ArgumentException`, null `ArgumentNullException`), written with a
`Utf8JsonWriter` and `JavaScriptEncoder.Default`, without reflection, so it suits NativeAOT. The encoder escapes `<`, `>`, `&` and
quotes in every name and value (`\u003C/script\u003E`), so a tour name that holds `</script>` or `<!--` can neither close the script nor
open a comment; a browser test parses it back. The JSON is written as raw markup, because HTML entities are not decoded inside a
script. Put it beside `PageHead`; it takes no class or attribute. Mark up only what the page shows.

```razor
<PageHead Title="@tour.Title" CanonicalUrl="@canonical" />
<JsonLd Data="@(new JsonObject { ["@context"] = "https://schema.org", ["@type"] = "TouristTrip", ["name"] = tour.Title })" />
```

### Image credits

An image under a licence such as Creative Commons must credit its author and licence. The components take display records, so the
application words every credit and chooses the links:

- `ImageCreditLine(Text, LicenseText, LicenseHref, CreditsHref)` is the short credit of one image, such as "Foto: Ana Lima" and
  "CC BY 4.0". Give it to a `GalleryImage` as `Credit` (`new GalleryImage(image, caption) { Credit = … }`): `ImageGrid`, and with it
  `AlbumSection` and a `StepList` step, renders it in the image's `figcaption`, after the caption (a `figcaption` is created for a
  credit alone) and outside the image's link, so the link keeps the alternative text as its name.
- `ImageCreditCaption` renders that line anywhere else, such as under a hero, from its one required `Credit`:
  `<small class="site-image-credit">`, a muted line of small text (the small typography role) on a line of its own. `CreditsHref`,
  such as the credits page, links the `Text`; the `LicenseText` follows after a space, linked to `LicenseHref` with `rel="license"`.
  Its links are underlined in its colour. On the primary colour of a band, a cover hero or a footer the credit and the image caption
  take the on-primary colour; a browser test measures 4.5:1 on the page and on a primary band.
- The record checks itself where a component receives it, so the caption and the grid refuse the same lines: a blank `Text`, or a
  `LicenseHref` without `LicenseText`, throws `ArgumentException` naming the caller's parameter (`Credit` on the caption, `Images` on
  the grid); a missing `Credit` throws `ArgumentNullException`.
- `ImageCreditLine.FromCredit(credit, format, provider, creditsHref)` builds the line of a shared `ImageCredit`
  (`PandaBear.SharedKernel.BuildingBlocks`; see its README): the text from your format with the placeholders of
  `ImageCredit.ToShortText` (`{0}` the author, as in "Foto: {0}"; leave the licence out, because it follows), then the licence's
  `Code` linked to its `Url`.
- `CreditsList` renders the full attributions, such as on a credits page, after the
  [Creative Commons recommended practices for attribution](https://wiki.creativecommons.org/wiki/Recommended_practices_for_attribution)
  (title, author, source, licence): a `ul role="list"` (`site-credits-list`; the role keeps the list in Safari without markers) of
  `CreditItem(Author, LicenseText)` entries, both required, with the optional `Thumbnail`, `Title`, `AuthorHref`, `SourceHref`,
  `LicenseHref` and `ChangesText` set as `init` properties. Each is an `li` of an optional lazy 5rem square thumbnail beside its lines,
  each a `p`: the `Title` linked to the `SourceHref`, the `Author` linked to the `AuthorHref` (or, without a title, to the
  `SourceHref`), the `LicenseText` linked to the licence's deed with `rel="license"` (`site-credits-list__license`), and the
  `ChangesText`, such as "Cropped". A long title breaks inside the line instead of scrolling. An empty list renders nothing; a null
  entry, a blank author or licence text, or an entry without a title that sets both `AuthorHref` and `SourceHref` (its one name cannot
  carry two links) throws `ArgumentException`. `CreditItem.FromCredit(credit, changesText)` builds an entry from a shared
  `ImageCredit`, with `changesText` only when the image was modified; add the thumbnail and the author's page with `with`.

```razor
<ImageGrid Images="@([new GalleryImage(photo, "O salar ao entardecer")
{
    Credit = ImageCreditLine.FromCredit(photoCredit, "Foto: {0}", CultureInfo.GetCultureInfo("pt-BR"),
        LinkTarget.FromString("/pt/creditos")),
}])" />
<CreditsList Items="_credits" />
```

### Reviewed photo credits

Use [ContentRights](../SharedKernel.ContentRights/README.md) for the reviewed attribution of each work. Use
`PhotoCreditItem` to supply a stable fragment id, a photo-specific title for headings and link names, and its
`PublicContentCredit`. Each photo can have its own terminology or rights. Supply localized field and link labels
through `PhotoCreditLabels`.
Existing BuildingBlocks image credits use the same projection through
[`ContentAttribution.FromImageCredit`](../SharedKernel.ContentRights/README.md#existing-image-credits).

`PhotoCredit` renders the full credit by default. With `Compact` it shows the supplied creator or credit line
and the applicable terms near the photo. Set `DetailsHref` to the same photo's entry on a credits page, for
example `LinkTarget.FromString("/credits#waterfall")`. Without the link, a native disclosure provides the complete
credit next to the photo. The full credit retains the reviewed original work title when supplied. The caller's
photo label names the credits entry and its links. An empty credit renders no markup. A record marked unknown can
show the supplied rights statement as text.

`PhotoCreditsPage` renders a section wherever the application places it. The application owns the route,
page title, coverage statement, localization and publication permission. Supply at most
`PhotoCreditsPage.MaxItems` credits in one result, and paginate the source for larger lists. Use the exact
item id in each gallery link. A page heading uses level 1 by default, and its photo headings use the next level.

```razor
<PhotoCreditsPage Title="@Texts.PhotoCredits" HeadingId="photo-credits"
                  Intro="@Texts.AlbumPhotoCreditScope" Items="@ApprovedCreditsOnPage" Labels="@CreditLabels" />
```

On a mixed-license gallery, give each reviewed credit its own terms label and address. The credit appears in
the same figure caption as the photo's caption. The image's alternative text and any full-image link continue
to describe the photo. Supply the caller-localized labels through `ImageGrid.CreditLabels` or
`AlbumSection.CreditLabels`.

```csharp
var waterfall = new PhotoCreditItem("waterfall", "Rainforest waterfall", reviewedWaterfallCredit)
{
    TermsLabel = "CC BY 4.0",
    DetailsHref = LinkTarget.FromString("/credits#waterfall"),
};
var ridge = new PhotoCreditItem("ridge", "Mountain ridge", reviewedArchiveCredit)
{
    TermsLabel = texts.ArchivePermission,
    DetailsHref = LinkTarget.FromString("/credits#ridge"),
};
GalleryImage[] photos =
[
    new GalleryImage(waterfallImage, texts.WaterfallCaption) { ReviewedCredit = waterfall },
    new GalleryImage(ridgeImage, texts.RidgeCaption) { ReviewedCredit = ridge },
];
PhotoCreditItem[] approvedCredits = [waterfall, ridge];
```

```razor
<ImageGrid Images="@photos" CreditLabels="@CreditLabels" />
```

Pass `approvedCredits` to the credits page. For a large catalogue, link each photo to the page of results
that contains its entry.

A scope introduction can say “These credits cover the photos shown in the Waterfalls album. Other photos and
site material may have separate terms.” Keep each creator, source, exact terms and change notice with the
corresponding photo. Review the supplied terms and any requested placement before choosing a compact credit,
a nearby full credit or a linked entry. Keep the image description and caption available for the image and
preserve any source credit requirements. [Creative Commons attribution guidance](https://wiki.creativecommons.org/wiki/Recommended_practices_for_attribution)

### Bands and photos

On the primary colour or a photo, text is the on-primary colour in the light scheme (`color-scheme: light`), whatever the visitor's
scheme: a `Primary` or `Image` band, a `Cover` hero and a `Primary` footer. Inside them headings (whatever their theme colour), the divider,
a plain card's eyebrow, links, a section's eyebrow and intro, the hero's
eyebrow and subtitle, a quote and a byline's job title take that colour, the focus ring is the on-primary colour (a card's ring included, while the rings inside a boxed card keep the focus colour on the card), a primary button
turns to the on-primary colour with primary text, and an outline button to an on-primary border and text; on hover a primary button
takes the primary-hover background, and the more look of a plain card follows its card's hover with the same inverted look. A boxed
card and the contact card keep the look of the page on their own background: their text colour, the card title's colour and the page's
buttons, more look included. A browser test measures these colours at rest and on hover.

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
  - absolute `http` and `https` addresses that `SharedKernel.BuildingBlocks.WebUrl` accepts: `//`, a host and no user information
    (`https:host`, `https://user:pw@host` and `https://@host` are refused). The target keeps the address exactly as supplied,
    not `WebUrl`'s lower-cased scheme and host;
  - `mailto:` with exactly one address and no query or headers (`?`, `&`, `;`, `,` and `%` are refused);
  - `tel:` with digits and an optional leading `+`.

  Every address is refused when it has white space anywhere (including `\u0085` and no-break space), a control character or a
  backslash, or is longer than `LinkTarget.MaxLength` (2048, the same as `WebUrl.MaxLength`). Root-relative checks reuse
  `SharedKernel.InputNormalization.WebAssetUriSanitizer`, which also refuses lone surrogates. Error messages never repeat the input.
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
- `ContactCard` takes `SharedKernel.BuildingBlocks.EmailAddress` and `PhoneNumber`. It checks restored values again, so only digits reach a
  `tel:` or WhatsApp link and no query reaches a `mailto:` link.
- `PageHead` addresses are `System.Uri` and must be absolute `http` or `https`.
- Cultures are `SharedKernel.BuildingBlocks.ContentCulture` (`LanguageLink.Culture`, `AlternateLink.Culture`, `PageHead.Locale`), so the
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
- A text brand changed look: a link of text in `SiteHeader`'s brand slot no longer takes the `Link` role (the body font, underlined);
  it takes the `Brand` typography role, with the h1 face, bold, without the underline. A theme that wants another look sets
  `typography.brand`; a logo link is not affected.

## Page methods

A site without forms answers GET and HEAD only. `MapRazorComponents<App>().AllowOnlyGetAndHead(typeof(Error))` lets every routed
component answer those two methods, so any other method gets 405 with `Allow: GET, HEAD`, and the error page every method: the error
handlers (`UseErrorPages` in `PandaBear.SharedKernel.AspNetCore`) re-execute a failed request with its own method, and routing matches
it before the page renders it as a GET.

## Parameter guards

The components check their parameters with three public guards, which a component of an application or another package can use for
the same rules and messages:

- `ElementId.Require(id, parameterName)` refuses an id that is empty or holds white space; `null` means no id.
- `PositionFormat.Require(format, parameterName, culture)` refuses a missing format (`ArgumentNullException`), a blank one, one that `string.Format` refuses (an
  `ArgumentException` with the `FormatException` inside) and one that writes positions 1 and 2 the same, such as `Day` or `Day {{0}}`;
  it returns the format applied to position 1. `StepList` checks `NumberFormat` and `HeadingIdFormat` with it.
- `EnumParameter.RequireDefined(value, parameterName, message)` refuses a value its enum does not declare, such as `(HeroLayout)9`,
  with an `ArgumentOutOfRangeException` that carries the message; the nullable overload accepts `null`.

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
| `--site-color-status-neutral`, `--site-color-on-status-neutral` | `#e5e7eb`, `#1f2937` / `#374151`, `#f3f4f6` | `StatusBadge` `Neutral`: background, text |
| `--site-color-status-info`, `--site-color-on-status-info` | `#dbeafe`, `#1e3a8a` / `#1e3a8a`, `#dbeafe` | `StatusBadge` `Info`: background, text |
| `--site-color-status-success`, `--site-color-on-status-success` | `#dcfce7`, `#14532d` / `#14532d`, `#dcfce7` | `StatusBadge` `Success`: background, text |
| `--site-color-status-warning`, `--site-color-on-status-warning` | `#fef3c7`, `#78350f` / `#78350f`, `#fef3c7` | `StatusBadge` `Warning`: background, text |
| `--site-color-status-danger`, `--site-color-on-status-danger` | `#fee2e2`, `#7f1d1d` / `#7f1d1d`, `#fee2e2` | `StatusBadge` `Danger`: background, text |
| `--site-focus-ring` | `3px solid` focus colour | `:focus-visible` outline and a card's ring |
| `--site-font-body` | system UI stack | Body font; no web font is loaded |
| `--site-font-heading` | `--site-font-body` | Heading font |
| `--site-font-size-sm` … `--site-font-size-3xl` | fluid `clamp()` scale | `--site-font-size-sm`, `--site-font-size-base`, `--site-font-size-lg`, `--site-font-size-xl`, `--site-font-size-2xl`, `--site-font-size-3xl` |
| `--site-line-height` | `1.6` | Body line height |
| `--site-space-1` … `--site-space-6` | `0.25rem` to `4rem` | `--site-space-1`, `--site-space-2`, `--site-space-3`, `--site-space-4`, `--site-space-5`, `--site-space-6` |
| `--site-radius` | `0.5rem` | Corner radius of surfaces: cards, disclosures, images, quotes; the theme's shape (see [Shape](#shape)) |
| `--site-control-radius` | `0.5rem` | Corner radius of controls: buttons, pager links, language links; the theme's shape |
| `--site-divider-width` | `4rem` | Width of the section divider and the hero rule; the theme's shape |
| `--site-divider-thickness` | `0.25rem` | Thickness of the section divider and the hero rule; the theme's shape |
| `--site-divider-color` | `--site-color-accent` | Colour of the section divider and the hero rule on the page; the theme's shape |
| `--site-content-width` | `72rem` | Maximum content width |
| `--site-card-min-width` | `16rem` | Smallest card column of `CardGrid`; `MinColumnWidth` sets it |
| `--site-card-max-width` | `none` | Largest card width in a column of `CardGrid`; `MaxColumnWidth` sets it |
| `--site-card-gap` | `--site-space-4` | Space between the cards of `CardGrid`; `Gap` sets it |
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
| `--site-hero-title-scale-large` | `1.25` | Factor of the level 1 heading size for a `Hero` with `TitleScale` `Large` |

Every default text pair reaches a WCAG contrast ratio of at least 4.5:1 in both schemes (text, muted and primary on surface and
surface-alt, accent on surface, on-primary on primary and on primary-hover, on-accent on accent and on accent-hover, primary-hover on
surface and surface-alt); a test computes the ratios for both sides of every `light-dark()` pair. Buttons change to the hover tokens
on hover, never to a filter, so the hover state keeps the checked contrast. Light
values: text 14.7:1, muted 7.6:1, primary 6.7:1 and accent 5.0:1 on white. The stylesheet respects `prefers-reduced-motion`, shows a
visible `:focus-visible` ring, is mobile first with a `48rem` layout breakpoint and a `60rem` header breakpoint, and loads no fonts, images or other URLs.

## Owner-editable theme

`SharedKernel.Blazor.Site.Theming` turns colours, typography and self-hosted fonts that a site owner edits into a second stylesheet
that `site.css` consumes. Without a theme nothing changes: every per-role variable falls back to the value `site.css` used before.

### Model

- `CssColor`: `#rrggbb` or `#rgb` (normalized to lower-case `#rrggbb`), with `RelativeLuminance` and `ContrastRatio(other)`
  (WCAG 2.2 formula, unrounded). `ThemeColor.Create(light, dark)` is one token for both schemes.
- `SiteColors.Create(primary, onPrimary, accent, onAccent, text, muted, surface, surfaceAlt, border, focus, primaryHover?, accentHover?)`.
  A hover colour that is not given is derived: its base mixed with 20% of the text colour per scheme (`IsDerived(role)`).
- `TypographyStyle.Create(fontKey, fallback, size, weight, lineHeight, letterSpacing, transform, italic, color)` for each `TypographyRole`
  (h1 to h6, body, small, link, button, nav, card title, brand). `CardTitle` styles every card heading (`.site-card__title`) whatever its
  level, so an uppercase or large section heading role does not reach the cards; by default it is a bold 1.125rem to 1.375rem heading
  without a transform, and its field path is `typography.cardTitle`. Its unset font family, weight and style come from the theme's own
  h3 before the default, so a stored theme that gives its headings a display font keeps it on card titles. `Brand` styles a link in the
  header's brand slot (`.site-header__brand a`), such as the site's name as text when there is no logo: by default it is a 1.125rem to
  1.375rem bold heading face in the primary colour without the link underline (which comes back on hover), its field path is
  `typography.brand`, and its unset font family, weight and style come from the theme's own h1, so the name takes the title's face; a
  browser test measures it and its focus ring. `Brand` is not a heading role, but it takes a colour like one; without a colour it
  keeps the primary colour (`color: var(--site-brand-color, var(--site-color-primary))`, in the site stylesheet and the theme
  preview alike), and over a hero the header gives it the on-primary colour whatever the theme sets. `color` colours a heading role
  (h1 to h6 and the card title) or the brand
  with the text, primary or muted colour, whose contrast on both surfaces the theme already checks; another colour is an error on
  `color`, and a colour on another role is an error on `typography.{role}.color`, because the components colour that text. Every value is optional; an unset value keeps `SiteTheme.Default`, and links, buttons
  and navigation inherit from their surroundings when unset (so a link in small print stays small). `FontSize.Fixed(rem)` or
  `FontSize.Fluid(minRem, maxRem)` (0.5 to 10rem); a fluid size grows linearly from a 20rem (320px) to an 80rem (1280px) viewport as
  `clamp(min, a rem + b vw, max)`. Weight: 100 to 900 in steps of 100. Line height: 0.8 to 3. Letter spacing: -0.1em to 0.5em.
  Transform: none, uppercase or capitalize.
- `ThemeFont.Create(key, family, faces)` with `ThemeFontFace.Create(weight, style, src)`. Fonts are self-hosted uploads: `src` is a
  `LinkTarget` that is root-relative or https and names a `.woff2` file, without `" ' ( ) < >` or a fragment. The family name allows
  letters, digits, single inner spaces, hyphens and underscores (at most 64 characters); the key is a lower-case slug. A role's
  `fallback` (`SansSerif`, `Serif`, `Monospace`) is the system stack written after the uploaded family.
- `SiteTheme.Create(colors, typography, fonts?, allowSmallText, brandHeight, colorLabel?, shape?)` enforces every rule;
  `SiteTheme.CreateDraft(...)` only requires unique font keys, existing font references and a positive brand height with at most four
  decimals, so a preview can show a theme that still breaks a rule. `brandHeight` defaults to `SiteTheme.DefaultBrandHeight` (2.5rem).
  `theme.Review(colorLabel?)` returns the errors, the warnings and every `ContrastCheck`. `SiteTheme.Default` is `site.css` as a
  theme. Every value object factory returns a `Result`; errors carry field paths such as `colors.muted`, `typography.h1.size`,
  `typography.body.lineHeight`, `fonts[0].key`, `header.brandHeight`.
- Contrast messages name the colours by their CSS names (`muted on surface-alt is 1.33:1 in the light scheme; …`). A host whose
  editor shows other names passes `colorLabel`, a `Func<ThemeColorRole, string?>` such as `role => role switch { ThemeColorRole.SurfaceAlt
  => "Card background", … }`, to `Review`, `Create`, `ThemeEditorModel.ToTheme` and `ThemePreview.ColorLabel`; the message then reads
  `Muted text on Card background is …`. A role it returns `null` or white space for keeps its CSS name; field paths do not change.

### Shape

`SiteShape.Create(control, dividerWidth, dividerThickness, dividerColor)` is the theme's shape; `SiteShape.Default` (rounded, a 4rem
accent divider 0.25rem thick) is `site.css` without a theme. `ControlShape` sets two radii, so every component follows one choice:

| `ControlShape` | `--site-control-radius` (buttons, the more look of a card, pager links, language links) | `--site-radius` (cards, disclosures, images, quotes, the contact card) |
| --- | --- | --- |
| `Rounded` (default) | `0.5rem` | `0.5rem` |
| `Square` | `0` | `0` |
| `Pill` | `999rem`: fully round ends | `0.5rem`: a card or a disclosure has no ends to round |

The divider is one rule in two places: `Section`'s divider under its heading and, with `Hero.Divider`, the hero's rule after the
eyebrow and before the subtitle (`site-hero--divided`; drawn by pseudo-elements, so the `hgroup` keeps only its `p` and `h1` and
assistive technology reads nothing; a hero with neither line has no rule). Both are `--site-divider-width` wide,
`--site-divider-thickness` thick and `--site-divider-color` on the page; on the primary colour or a photo they take the text's colour.
The divider colour is the primary, accent, text, muted or border colour (an error on `dividerColor` otherwise); it is decorative, so no
contrast applies. Width and thickness are positive rem values with at most four decimals (`dividerWidth`, `dividerThickness`), and the
rules below keep them in a layout range. `ThemeEditorModel` has `ControlShape`, `DividerWidth`, `DividerThickness` and `DividerColor`
(empty means the default) and reports errors on `shape.{field}`.

A button's padding is computed from its own typography, not picked: `max(0.25em, (2.75em - 1lh) / 2) 1.375em`
(`SiteThemeStylesheet.ButtonPadding` in the preview), so a button is 2.75 times its font size tall whatever its line height, its
inline padding is half that height and a pill's round end never reaches the text, and it is at least 2.75rem (44px) tall as a tap
target. `lh` needs a 2023 or later browser (Chrome 109, Firefox 120, Safari 16.4).

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
| Divider width | 1rem to 16rem | Error on `shape.dividerWidth` | layout policy: a shorter rule reads as a dot, a longer one as a border |
| Divider thickness | 0.0625rem (1px) to 1rem | Error on `shape.dividerThickness` | layout policy: a thicker rule reads as a bar |
| Heading colour | text, primary or muted, on h1 to h6, the card title and the brand only | Error on `typography.{role}.color` | WCAG 2.2 SC 1.4.3: only colours whose 4.5:1 on both surfaces the theme checks |
| Fluid size | maximum at most 2.5 times the minimum | Error | WCAG 2.2 SC 1.4.4 (zoom must still double the text) |
| Heading order: each level not larger than the level above, minimum and maximum compared | — | Warning on the smaller level's `size` | design guidance |
| Font references and keys | key exists, keys unique | Error, also for drafts | rendering |

Headings use the text colour or a heading colour (text, primary or muted), each of which already needs 4.5:1, so the 3:1 large-text allowance of SC 1.4.3 never lowers a check. Borders
are not checked: the components never rely on a border alone to identify a control (SC 1.4.11 applies to the focus indicator here),
and the default `#d1d5db` on white is a decorative 1.5:1. Heading order is a warning, not an error: WCAG conveys structure through
heading markup (SC 1.3.1), not size, and a design can mark a level with weight, case or spacing instead.

### Stylesheet

`SiteThemeStylesheet.Render(theme)` writes `@font-face` rules (`font-display: swap`, key then weight order), then `:root` with the 12
colour tokens as `light-dark(light, dark)`, the three scrim tokens as percentages (`SiteThemeStylesheet.ScrimVariables`), the brand and header heights in rem
(`SiteThemeStylesheet.HeaderVariables`: `--site-brand-height`, `--site-header-height`), the shape (`SiteThemeStylesheet.ShapeVariables`:
`--site-radius`, `--site-control-radius`, `--site-divider-width`, `--site-divider-thickness`, `--site-divider-color`), the set
typography values as `--site-{role}-{property}`, where role is `h1` … `h6`,
`body`, `small`, `link`, `button`, `nav`, `card-title` or `brand`, and the colour of a heading role that sets one as `--site-{role}-color`
(`SiteThemeStylesheet.HeadingColorVariables`, such as `--site-h2-color: var(--site-color-primary)`), and property is `font-family`, `font-size`, `font-weight`, `line-height`,
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
button, a card whose title has the card title role; the buttons and the card take the theme's shape and the site's button padding) styled by a `style` element scoped to `ScopeId`, then a contrast table for the shown scheme (pair, ratio rounded
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

`SharedKernel.Branding.BrandingSettings` (the ViajantesTurismo Branding context) maps as follows: `PrimaryColor` to `primary`,
`AccentColor` to `accent`, `BackgroundColor` to `surface`, `TextColor` to `text`, each as the light value with a dark value chosen by
the owner (or `SiteTheme.Default`'s); on-primary, on-accent, muted, surface-alt, border and focus come from `SiteTheme.Default` until
edited, and `Create` reports any pair that the brand colours break. `HeadingFontFamily` and `BodyFontFamily` become the font keys of
h1 to h6 and body; a system family (`system-ui`, `Arial`, `Verdana`, `Georgia`) maps to no key with the `SansSerif` or `Serif`
fallback, and `Inter` maps to an uploaded `ThemeFont`. `BrandName` and `LogoUri` are not part of the theme.

### Storage document

`ThemeDocument` is a theme as plain, versioned data: the shape a host stores (one JSON column or file, read and written whole, never
queried) or embeds in an import file. `ThemeDocumentJson.Serialize(theme)` writes it and `ThemeDocumentJson.Restore(json)` reads it back:

```json
{"version":1,"colors":{"primary":{"light":"#1d4ed8","dark":"#93c5fd"}},"typography":{"h1":{"fallback":"SansSerif","sizeMin":2.125,"sizeMax":3.5,"weight":700}},"fonts":[{"key":"noto-sans","family":"Noto Sans","faces":[{"weight":400,"style":"Normal","src":"/media/noto.woff2"}]}],"allowSmallText":false,"header":{"brandHeight":2.5},"shape":{"control":"Pill","dividerWidth":4,"dividerThickness":0.25,"dividerColor":"Accent"}}
```

- Members are camel case and read case-insensitively; colours and typography are keyed by the field-path role names
  (`ThemeDocument.RoleName`: `surfaceAlt`, `cardTitle`); enums are written by member name and a number is refused; an empty value is
  left out; a member the format does not define is ignored. A derived hover colour and an unset typography role are left out, so they
  stay derived and default after a reload.
- `version` comes first. `ThemeDocument.CurrentVersion` is 1, the first format, which ViajantesTurismo stored before the format had a
  version: text without `version` reads as version 1, and the tests keep that stored text loadable byte for byte (a new save is the
  same text with `"version":1,` in front). A document of another version is refused, by `ToEditorModel` and `ToTheme` on the field
  `version` and by `Restore` with an `InvalidOperationException`; a future format change raises the version and keeps reading the
  older ones.
- `ThemeDocument.From(theme)` and `From(model)` describe a theme or an editor's fields as they are; `ToEditorModel()` fills a
  `ThemeEditorModel` (an unknown role is an error on `colors` or `typography`); `ToTheme(colorLabel?)` enforces every rule, for a save or
  an import; `Restore()` checks only what rendering needs (`ToDraft`), because a stored theme met the rules when it was saved and must
  still load after a rule tightens.
- A host can embed `ThemeDocument` in its own JSON (an import file's `theme` section) with its own serializer options; enable enums by
  name there too.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Blazor.Site`. C# namespace: `SharedKernel.Blazor.Site`. Assembly/project identity: `SharedKernel.Blazor.Site`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
