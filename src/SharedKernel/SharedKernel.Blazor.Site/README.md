# SharedKernel.Blazor.Site

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Brand-neutral, accessible Razor components for a public website, plus one stylesheet of design tokens. The components use semantic
landmarks, need no JavaScript and no interactive render mode, and work with static server-side rendering. Every component except
`PageHead` takes a `class` and passes any other unmatched attribute to its root element.

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
    <Hero Title="Small-group tours" Subtitle="Eight countries, one guide."
          ImageSrc="@("/img/hero.jpg")" ImageAlt="@ImageAlternative.Describe("A lake below snowy peaks")" ImageWidth="1600" ImageHeight="900">
        <Actions><ButtonLink Href="@("/tours")">See the tours</ButtonLink></Actions>
    </Hero>
    <Section Title="Tours" Intro="Pick your next trip.">
        <CardGrid>
            <Card Title="Atacama" Href="@("/tours/atacama")" ImageSrc="@("/img/atacama.jpg")" ImageAlt="@ImageAlternative.Decorative()">
                <p>Eight days in the desert.</p>
            </Card>
        </CardGrid>
    </Section>
</main>
<SiteFooter><ContactCard Email="_email" Phone="_phone" WhatsApp="_phone" /></SiteFooter>

@code {
    private static readonly SiteNavItem[] _nav =
    [
        new("Tours", "/tours"),
        new("About", Children: [new("Team", "/about/team"), new("Story", "/about/story")]),
    ];
}
```

The stylesheet is a static web asset. Its path is `_content/` plus the package id plus `/site.css`:
`_content/PandaBear.SharedKernel.Blazor.Site/site.css`. An app that maps assets with `MapStaticAssets` can write
`@Assets["_content/PandaBear.SharedKernel.Blazor.Site/site.css"]` to get the fingerprinted address.

## Components

| Component | Renders |
| --- | --- |
| `SkipLink` | The first link of the page; `TargetId` defaults to `main-content`. |
| `SiteHeader` | `header` with a brand slot, a labelled `nav`, and an actions slot. An item with children is a native `details`; the item that matches `CurrentPath` (ignoring case, query, fragment and a trailing slash) gets `aria-current="page"` and opens its group. |
| `SiteFooter` | `footer` with a content slot and an optional `Legal` line. |
| `Hero` | The page's one `h1`, a subtitle, an eager image and an actions slot. |
| `Section` | `section` labelled by its heading (`Level`, default 2), a decorative divider and an intro. |
| `ButtonLink` | A link styled as a button (`Primary`, `Secondary`, `Outline`). `NewTab` adds `target="_blank"`, `rel="noopener noreferrer"` and a visually hidden `NewTabHint`. |
| `Card`, `CardGrid` | `article` with an optional lazy image, a heading (`HeadingLevel`, default 3) whose link covers the card, text and actions; an auto-fit grid. |
| `FactsTable` | `table` with a required `caption` and `th scope="row"` headers. |
| `Disclosure` | Native `details` and `summary`; a tab substitute without script. |
| `Quote` | `figure` with a `blockquote` and a `cite` caption. |
| `ImageGrid` | `figure` per image with width and height, lazy loading except the first `EagerCount`. |
| `ContactCard` | `address` with postal lines and `tel:`, `mailto:` and `https://wa.me/` links. |
| `LanguageSwitcher` | Labelled `nav` of links with `hreflang`, `lang` and `aria-current="true"` on the current language. |
| `PageHead` | Title, description, canonical link, OpenGraph basics and `hreflang` alternates including `x-default`. |

Texts that people read (labels, the new-tab hint, the WhatsApp link text) are parameters so the caller can localize them.

## Value objects

- `LinkTarget` is every link and image address. `LinkTarget.Create` returns a `Result`; only relative addresses and the `http`, `https`,
  `mailto` and `tel` schemes are accepted, and script or data schemes, protocol-relative addresses, backslashes and control characters
  are refused. Text converts implicitly (through `FromString`, which throws `ArgumentException`), so author-controlled markup can write
  `Href="@("/tours")"` (a bare `Href="/tours"` is not C# and does not compile); build addresses from external data with `Create`.
- `ImageAlternative` is the accessibility decision for an image: `ImageAlternative.Describe("what it shows")` or
  `ImageAlternative.Decorative()` (rendered as `alt=""`). A component given an image source without a decision throws
  `InvalidOperationException` when it renders, and `GalleryImage` requires one.
- `ContactCard` takes `PandaBear.BuildingBlocks.EmailAddress` and `PhoneNumber`. It checks restored values again, so only digits reach a
  `tel:` or WhatsApp link and no query reaches a `mailto:` link.
- `PageHead` addresses are `System.Uri` and must be absolute `http` or `https`.
- Cultures are `PandaBear.BuildingBlocks.ContentCulture` (`LanguageLink.Culture`, `AlternateLink.Culture`, `PageHead.Locale`), so the
  language-tag rule and canonical casing (`pt-br` becomes `pt-BR`) live in one place. `hreflang` and `lang` use the canonical name;
  `og:locale` writes it with an underscore (`pt_BR`). Build cultures from input with `ContentCulture.Create`; `Restore` skips validation.

## Design tokens

Override any token on `:root` in your own stylesheet. The dark values apply under `prefers-color-scheme: dark`.

| Token | Default (light / dark) | Use |
| --- | --- | --- |
| `--site-color-primary` | `#1d4ed8` / `#93c5fd` | Links, primary buttons, current items |
| `--site-color-on-primary` | `#ffffff` / `#111827` | Text on the primary colour |
| `--site-color-surface` | `#ffffff` / `#111827` | Page background |
| `--site-color-surface-alt` | `#f3f4f6` / `#1f2937` | Cards, quotes, contact card |
| `--site-color-text` | `#1f2937` / `#f3f4f6` | Body text |
| `--site-color-muted` | `#4b5563` / `#9ca3af` | Secondary text |
| `--site-color-border` | `#d1d5db` / `#374151` | Borders and rules |
| `--site-color-accent` | `#b45309` / `#fbbf24` | Divider, secondary buttons |
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

Every default text pair reaches a WCAG contrast ratio of at least 4.5:1 in both schemes (text, muted and primary on surface and
surface-alt, accent on surface, on-primary on primary, on-accent on accent); a test computes the ratios from the stylesheet. Light
values: text 14.7:1, muted 7.6:1, primary 6.7:1 and accent 5.0:1 on white. The stylesheet respects `prefers-reduced-motion`, shows a
visible `:focus-visible` ring, is mobile first with one `48rem` breakpoint, and loads no fonts, images or other URLs.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Blazor.Site`. C# namespace: `PandaBear.Blazor.Site`. Assembly/project identity: `SharedKernel.Blazor.Site`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
