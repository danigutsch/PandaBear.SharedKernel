# PandaBear.SharedKernel.PageSections.Blazor

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Renders the typed sections of `PandaBear.SharedKernel.PageSections` with the components of `PandaBear.SharedKernel.Blazor.Site`: each
section on the band of its tone, its parts in a fixed order, its images as a mosaic or beside the text. It works with static server-side
rendering, needs no script, and holds no copy of its own: every word on the page comes from the sections or from the application.

## Install

Replace the placeholders with an actually validated candidate version and its local/private feed. Registry publication remains deferred.

```text
dotnet add package PandaBear.SharedKernel.PageSections.Blazor --version <validated-candidate-version> --source <candidate-feed>
```

Reference the site stylesheet as `PandaBear.SharedKernel.Blazor.Site` describes; this package adds no stylesheet.

## Usage

`PageSectionsView<TImageId, TLink, TExtra>` takes the sections of one translation and two resolvers that the application owns:

```razor
@using PandaBear.PageSections.Blazor

<PageSectionsView Sections="_page.Sections" Image="Images.Responsive" Link="@(target => Routes.Of(target, Language))"
                  VideoSiteHintFormat="@Text["VideoSiteHintFormat"]" TextLanguage="@_page.FallbackLanguage"
                  TextAlignment="SiteAlignment.Center" IsShown="@(section => section.Extra.Condition.Holds(_tour))">
    <Extra Context="extra"><SettingsBlock Block="extra.Block" Settings="_settings" /></Extra>
</PageSectionsView>
```

| Parameter | Meaning |
| --- | --- |
| `Sections` | The sections, in page order. Required. |
| `Image` | `Func<TImageId, ResponsiveImage?>`: an image id as an image. `null` leaves that image out. Required. |
| `Link` | `Func<TLink, LinkTarget?>`: a validated link target as an address, such as a page's path in the current language. `null` leaves the button out. Required. |
| `Extra` | `RenderFragment<TExtra>`: the application's part, for a section whose `Extra` is not `default`. Without it, extensions render nothing and do not count as content. |
| `IsShown` | `Func<PageSection<…>, bool>`: which sections are shown, for example by a display condition in the extension. Unset shows every section. |
| `HeadingIdFormat` | The composite format of a heading's id, `{0}` the section's position on the page from 1; `section-{0}` by default (`DefaultHeadingIdFormat`). A hidden section keeps its position, so the other ids never move. A format that gives every section the same id (no `{0}`, or an escaped `{{0}}`) or an id with white space throws `ArgumentException`. |
| `VideoSiteHintFormat` | The visually hidden hint after a video's title, `{0}` the video's host without `www.`: `"(opens {0})"` reads "(opens youtube.com)". Required, in the language of the text. |
| `TextLanguage` | A `ContentCulture` written as `lang` on the element around the sections when their text is not in the page's language, such as a fallback translation (WCAG 3.1.2). |
| `TextAlignment` | `Band.TextAlignment` of every band: `null` inherits, `Center` centres the headings, text, byline, video link and button. |
| `SplitAlignment` | `Split.Alignment` of a section without a heading whose images stand beside its text: `Start` (default) or `Center`, for a short tagline beside a photo. A section with a heading starts both columns at the top. |

## Output

The sections are inside one `div` (with `lang` when `TextLanguage` is set). For each shown section, in this order:

1. a `Band` of the section's tone: `Plain` is `Default`, `Muted` and `Primary` are their own, and `Image` is an `Image` band over the
   background, or a `Primary` band when the background does not resolve;
2. with a heading, a `Section` named by it (`aria-labelledby`), with the eyebrow in an `hgroup` above it; a `Subsection` has a level-three
   heading and no divider. Without a heading the parts stand on the band, never a landmark;
3. the paragraphs, each a `p`;
4. the questions, each a closed `Disclosure` whose answer is its paragraphs;
5. the person, a `Byline` with the photo when it resolves;
6. the quotes, each a `Quote`;
7. the application's `Extra`;
8. the video, a `VideoLink` to its https address with the poster shown as decoration (the title names the link); it is left out when the
   poster does not resolve or the stored address is not a valid link. The poster must be self-hosted at root-relative addresses, as `VideoLink` requires;
9. the button, a `ButtonLink` in a `p`, its `LinkStyle` (`Primary`, `Secondary`, `Outline`) as the `ButtonVariant` of the same name; it
   is left out when the target does not resolve;
10. the images that resolve, an `ImageGrid` in the `Mosaic` layout, after the parts; with `BesideText` they stand beside the parts in
    the section's `Split` (`Section.Media`), or in a `Split` of their own without a heading. A section whose only part is its images shows
    them alone, without an empty column.

A section with nothing left to show (every part empty or left out) renders nothing, not even its band.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.PageSections.Blazor`. C# namespace: `PandaBear.PageSections.Blazor`. Assembly/project identity:
`SharedKernel.PageSections.Blazor`. It references `PandaBear.SharedKernel.PageSections` and `PandaBear.SharedKernel.Blazor.Site`, and
the ASP.NET Core shared framework, as the site package does. The component is named `PageSectionsView` because a type named
`PageSections` would clash with the `PandaBear.PageSections` namespace (CA1724).

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
