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
    <Hero Title="Small-group tours" Subtitle="Eight countries, one guide."
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
| `SiteHeader` | `header` with a brand slot, a labelled `nav` (`Label` is required when there are items), and an actions slot. An item has either an `Href` or one level of children; an item with children is a native `details`. The item that matches `CurrentPath` gets `aria-current="page"` and opens its group. |
| `SiteFooter` | `footer` with a content slot and an optional `Legal` line. |
| `Hero` | The page's one `h1`, a subtitle, an image loaded eagerly with `fetchpriority="high"`, and an actions slot. |
| `Section` | `section` with a heading (`Level`, default 2), a decorative divider and an intro. Set `HeadingId` to give the heading that id and label the section by it (`aria-labelledby`), which makes it a named region; without it no id is generated, so prerendered and interactive markup match. |
| `ButtonLink` | A link styled as a button (`Primary`, `Secondary`, `Outline`). `NewTab` adds `target="_blank"`, `rel="noopener noreferrer"` and a visually hidden `NewTabHint`. |
| `Card`, `CardGrid` | `article` with an optional lazy image, a heading (`HeadingLevel`, default 3) whose link covers the card, text and actions; an auto-fit grid. Links, buttons, form controls, `summary` and `[tabindex]` elements in the body and the actions are raised above the covering link, so they stay clickable; any other interactive element you put in the body needs `position: relative; z-index: 1`. The focus ring is drawn on the card while a control in it has keyboard focus (`:has(:focus-visible)`). |
| `FactsTable` | `table` with a required `caption` and `th scope="row"` headers. |
| `Disclosure` | Native `details` and `summary`; a tab substitute without script. |
| `Quote` | `figure` with a `blockquote` and a `cite` caption. |
| `ImageGrid` | `figure` per `GalleryImage` (a `ResponsiveImage` and an optional caption), lazy loading except the first `EagerCount`; `PrioritizeFirstImage` loads the first one eagerly with `fetchpriority="high"`. |
| `ContactCard` | `address` with postal lines and `tel:`, `mailto:` and `https://wa.me/` links; no `address` when there are no lines and no channels. `PhoneText` shows the number as written (`+55 (11) 99999-0000`) while the link uses the digits; it must be the same number as `Phone`. The WhatsApp link shows `WhatsAppLabel`. |
| `LanguageSwitcher` | Labelled `nav` of links with `hreflang`, `lang` and `aria-current="true"` on the current language. |
| `PageHead` | Title, description, canonical link, OpenGraph basics and `hreflang` alternates including `x-default`. |

`SiteHeader.CurrentPath` matching compares paths only: the query and fragment are ignored, a missing leading slash is added
(`Navigation.ToBaseRelativePath(...)` returns `tours`, which matches `/tours`), a trailing slash is ignored, and the comparison is
ordinal and ignores case. The empty path matches `/`. An item with an absolute address never matches.

Texts that people read (labels, the new-tab hint, the WhatsApp link text) are parameters so the caller can localize them.

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

Every default text pair reaches a WCAG contrast ratio of at least 4.5:1 in both schemes (text, muted and primary on surface and
surface-alt, accent on surface, on-primary on primary and on primary-hover, on-accent on accent and on accent-hover, primary-hover on
surface and surface-alt); a test computes the ratios for both sides of every `light-dark()` pair. Buttons change to the hover tokens
on hover, never to a filter, so the hover state keeps the checked contrast. Light
values: text 14.7:1, muted 7.6:1, primary 6.7:1 and accent 5.0:1 on white. The stylesheet respects `prefers-reduced-motion`, shows a
visible `:focus-visible` ring, is mobile first with one `48rem` breakpoint, and loads no fonts, images or other URLs.

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
- `SiteTheme.Create(colors, typography, fonts?, allowSmallText)` enforces every rule; `SiteTheme.CreateDraft(...)` only requires
  unique font keys and existing font references, so a preview can show a theme that still breaks a rule. `theme.Review()` returns the
  errors, the warnings and every `ContrastCheck`. `SiteTheme.Default` is `site.css` as a theme. Every value object factory returns a
  `Result`; errors carry field paths such as `colors.muted`, `typography.h1.size`, `typography.body.lineHeight`, `fonts[0].key`.

### Rules

| Rule | Threshold | Severity | Source |
| --- | --- | --- | --- |
| Text pairs: text, muted, primary and primary-hover on surface and surface-alt; on-primary on primary and primary-hover; on-accent on accent and accent-hover | 4.5:1 in the light and the dark scheme | Error on `colors.{foreground}` | WCAG 2.2 SC 1.4.3 |
| Focus ring on surface and surface-alt | 3:1 in both schemes | Error on `colors.focus` | WCAG 2.2 SC 1.4.11 |
| Body size (smallest value of a fluid size) | at least 1rem unless `allowSmallText` | Error | readability policy |
| Every other role with a size | at least 0.875rem unless `allowSmallText` | Error | readability policy |
| Body line height | 1.3 to 2 | Error | readability policy |
| Fluid size | maximum at most 2.5 times the minimum | Error | WCAG 2.2 SC 1.4.4 (zoom must still double the text) |
| Heading order: each level not larger than the level above, minimum and maximum compared | — | Warning on the smaller level's `size` | design guidance |
| Font references and keys | key exists, keys unique | Error, also for drafts | rendering |

Headings use the text colour, which already needs 4.5:1, so the 3:1 large-text allowance of SC 1.4.3 never lowers a check. Borders
are not checked: the components never rely on a border alone to identify a control (SC 1.4.11 applies to the focus indicator here),
and the default `#d1d5db` on white is a decorative 1.5:1. Heading order is a warning, not an error: WCAG conveys structure through
heading markup (SC 1.3.1), not size, and a design can mark a level with weight, case or spacing instead.

### Stylesheet

`SiteThemeStylesheet.Render(theme)` writes `@font-face` rules (`font-display: swap`, key then weight order), then `:root` with the 12
colour tokens as `light-dark(light, dark)` and the set typography values as `--site-{role}-{property}`, where role is `h1` … `h6`,
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

`ThemePreview` shows a small sample (navigation strip, h1 to h3, a paragraph with a link, small print, a primary and a secondary
button, a card) styled by a `style` element scoped to `ScopeId`, then a contrast table for the shown scheme (pair, ratio rounded
down, minimum, `Pass` or `Fail` in text) and the errors and warnings by field path. The sample is `inert`, so its headings and links
stay out of the page outline and tab order. It needs no script and re-renders on every parameter change:

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

The inline `style` element needs `style-src 'unsafe-inline'` (or a nonce) in the Content-Security-Policy of the administration host
that shows the preview. The public site keeps its strict policy because it loads `/theme.css` from a URL.

`ThemeEditorModel` is the form model: string colour fields (`colors.primary.light`), nullable numbers for sizes, weight and spacing,
and fonts with faces. `ToDraft()` and `ToTheme()` return every error at once on its field path. Both hover fields empty means derived.

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
