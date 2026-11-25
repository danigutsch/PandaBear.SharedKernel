# PandaBear.SharedKernel.PageSections

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Typed page sections for a public site: each section is composed from typed parts instead of styled by hand, sits on the band of its tone
and shows its parts in a fixed order. It holds the section model and its validation, with an extension point for the application's own
part. It depends only on `PandaBear.SharedKernel.BuildingBlocks`. The page itself (its key, slug, status, review and SEO fields) stays in
the application. `PandaBear.SharedKernel.PageSections.Blazor` renders the sections with the accessible site components
([README](../SharedKernel.PageSections.Blazor/README.md)); the model needs no display package.

## Install

Replace the placeholders with an actually validated candidate version and its local/private feed. Registry publication remains deferred.

```text
dotnet add package PandaBear.SharedKernel.PageSections --version <validated-candidate-version> --source <candidate-feed>
```

## Model

`PageSection<TImageId, TLink, TExtra>` is generic over the image id, the application's validated link target `TLink` (ViajantesTurismo
V2: its `NavTarget`, an opaque type with a private constructor and a `Result` factory; another application: its public route) and its
extension `TExtra : struct, IEquatable<TExtra>`, where `default` means none. An application without an extension uses `NoExtra`. V2's
extension is a settings block (contact, legal or bank details) and a display condition. The input side adds the raw link target
`TLinkInput` (a class, such as V2's kind, page key, tour and address fields): `PageSectionDefinition<TImageId, TLinkInput, TExtra>` and
`PageLinkDefinition<TLinkInput>` hold it, and the context turns it into a `TLink`.

| Part | Content |
| --- | --- |
| `Tone`, `BackgroundImageId` | `Plain` (default), `Muted`, `Primary`, or `Image` over a background image, which only `Image` has |
| `Eyebrow`, `Heading`, `Level` | a short line above the heading; the heading; `Section` (an `h2`, default) or `Subsection` (an `h3` under the section before it) |
| `Paragraphs` | the text, split into paragraphs on blank lines |
| `Questions` | `PageQuestion(Question, Answer)`, the answer as paragraphs |
| `Person` | `PagePerson(ImageId?, Name, Role?)` |
| `Quotes` | `PageQuote(Text, Cite?)` |
| `Extra` | the application's extension |
| `ImageIds`, `ImageLayout` | images in order, as a `Mosaic` (default) or `BesideText` |
| `Video` | `PageVideo(Address, PosterImageId, Title)`: an https address and a poster that links to it; the video is never embedded |
| `Link` | `PageLink(Text, Target, Style)`, the button; `LinkStyle` is `Primary` (default), `Secondary` or `Outline` |

A renderer shows them in this order: the band, the eyebrow and heading, the paragraphs, the questions, the person, the quotes, the
extension, the video and the button; the images follow as a mosaic or stand beside the rest. `LinkStyle` mirrors the button variants of
`PandaBear.SharedKernel.Blazor.Site`, so the model needs no display package. `ImagesShown()` lists the background, the person's photo,
the images and the poster, so the application can refuse to delete an image a section shows. Two sections with the same data are equal.

## Validation

`PageSectionList.Create(definitions, rules, context, scope)` validates the `PageSectionDefinition`s of one translation and returns the
sections or every error at once. `PageSectionScope(Culture, Published, FieldPrefix, ImagesField)` names the translation and the error
paths. The rules are ViajantesTurismo V2's:

- a section shows more than its heading: text, questions, a person, a quote, images, a video, a button, or an extension the context
  counts as content;
- a subsection needs a heading and follows a section with a heading, so the page's headings never skip a level;
- tone and background agree, and every image (background, photo, images, poster) exists; a section's images appear once;
- a button needs text and a target (a missing target is an error on the button's path, `….link`);
- a published translation has at least one section and, once everything else is valid, every image its sections show is eligible in
  its culture: one error on `scope.ImagesField` (V2's `pt.images`, from `PageSectionFields.Images(culture)`) that counts them. Set
  `CheckImageEligibility = false` to run `PageSectionList.FindIneligible` yourself, together with your own images such as a page cover,
  and word your own message (V2 names the files).

`PageSectionRules` holds the limits, V2's by default: 60 sections, 30 questions, 60 quotes, 60 images, 50 paragraphs of 4000
characters, 200 characters for a heading, a person's name and role, a quote's source, a video's title and a button's text, 300 for an
eyebrow and a question, and 1000 for a quote.

`IPageSectionContext<TImageId, TLinkInput, TLink, TExtra>` is the application's part: it extends BuildingBlocks' `IImageEligibility` with
`ValidateLink(input, field)`, which turns raw input into a validated target or returns its errors, `ValidateExtra(extra, published,
field)` and `CountsAsContent(extra)`. The last two have default bodies (accept unchanged; never content), so an application without an
extension writes only `ValidateLink` and the image knowledge. V2 checks there that a published section's settings block has its
settings, and refuses any condition but `Always` outside its `tour-information` page.

Errors are at V2's field paths (`PageSectionFields`): a section's own under `{fieldPrefix}[i]`, such as `pt.sections[0].heading`,
`pt.sections[0].questions[1].answer`, `pt.sections[0].person.name`, `pt.sections[0].quotes[2].cite`, `pt.sections[0].video.posterImageId`
or `pt.sections[0].link.text`; the extension's and the target's where the context puts them, under the section's or the button's path; a
section that shows nothing on the section's path; too many sections, or none to publish, on the prefix; ineligible images on
`{culture}.images`. The messages are English and never echo the input.

## Storing sections

`section.ToDefinition(linkInput)` gives a section back as a definition, each target turned back into raw input by `linkInput` and
paragraphs joined with blank lines; store it, never the entered definition. `PageSection.Restore(definition, restoreLink)` rebuilds a
stored one for a persistence mapper, `restoreLink` rebuilding each target without validation (as V2's target type's own restore does). Restore is structural only: it refuses what rendering cannot handle (`ArgumentException`:
an undefined level, tone, layout or button style, a question without an answer, a quote, person, button or video missing a required
value, malformed text) and runs no limits, image, context or publish rules, so never use it for input.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.PageSections`. C# namespace: `SharedKernel.PageSections`. Assembly/project identity: `SharedKernel.PageSections`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
