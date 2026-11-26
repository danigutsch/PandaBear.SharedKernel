# Content rights and attribution

`PandaBear.SharedKernel.ContentRights` records the rights and credit supplied for a work such as a photo,
an illustration, or an excerpt. It depends on BuildingBlocks and works independently of any browser
framework, image format, storage provider or application policy.

## Record a sourced work

```csharp
using SharedKernel.ContentRights;

var declaration = ContentLicense.Create(
    ContentLicenseKind.CcBy,
    declaredTerms: "Creative Commons Attribution 4.0 International",
    termsUrl: "https://creativecommons.org/licenses/by/4.0/legalcode.en");

if (declaration.TryGetValue(out var license))
{
    var attribution = ContentAttribution.Create(new ContentAttributionDefinition
    {
        Title = "Sunrise over Serra",
        Parties =
        [
            new ContentCreditParty("Alex Silva", ContentCreditRole.Creator, ContentCreditEntityKind.Person),
            new ContentCreditParty("Mountain Archive", ContentCreditRole.CopyrightOwner, ContentCreditEntityKind.Organization),
        ],
        OriginalSourceUrl = "https://archive.example.org/photos/serra",
        CreditLine = "Photo: Alex Silva / Mountain Archive",
        CopyrightNotice = "© Mountain Archive, 2024",
        PreviousModificationNotices = ["Cropped by Mountain Archive."],
        ModificationNotice = "Color corrected for this edition.",
        License = license,
    });

    if (attribution.TryGetValue(out var recorded))
    {
        PublicContentCredit credit = recorded.ToPublicCredit();
        // Display the selected, reviewed fields as escaped text and links.
    }
}
```

One way to display this data is [Sunrise over Serra](https://archive.example.org/photos/serra),
Photo: Alex Silva / Mountain Archive, © Mountain Archive, 2024, under
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Cropped by Mountain Archive.
Color corrected for this edition.

| Field | When to supply it |
| --- | --- |
| Title, creator and designated parties | The source provides a title or names. Keep these separate from image alt text and captions. |
| Copyright owner and licensor | The source identifies the owner or the terms giver separately. Specify the entity kind only when known. |
| Original source | The public source of the original work, often a work or collection page. A served image's address belongs to the renderer. |
| Credit line and copyright notice | The source supplies a requested credit or copyright notice. Carry forward the supplied wording. |
| License kind, declared terms and terms URL | You have evidence of a specific declaration. Supply its precise wording and published terms address when available. |
| Modification notices | The source supplies earlier change notices or your use changes the work. Carry earlier notices forward in order. |

`DeclaredOriginalSourceUrl` and `DeclaredTermsUrl` preserve the supplied spelling.
`OriginalSourceUrl` and `TermsUrl` are the corresponding validated absolute HTTP(S) addresses,
with normalized scheme and host. `CanonicalTermsUrl` identifies a recognized standard tool
with its actual version and, when present, port. For example, a supplied German deed
`http://creativecommons.org/licenses/by-sa/3.0/de/deed.de` resolves to
`https://creativecommons.org/licenses/by-sa/3.0/de/`. The exact supplied deed remains recorded.
Canonicalization drops presentation suffixes and tracking parameters, while keeping the stated legal tool.

## Recognized declarations

The six Creative Commons attribution families use their official versioned terms URLs.
The catalog includes generic and ported versions 1.0 to 3.0 where published and the international 4.0 suite.
For example, the 2.1 suite uses published ports and the legacy 1.0 Attribution-NonCommercial-NoDerivatives
URL uses `by-nd-nc`. Port and version recognition uses the
[Creative Commons legal tool data snapshot](https://github.com/creativecommons/cc-legal-tools-data/tree/91780e140892982eb3e0d530591ccbbca0d4690d/legacy/legalcode).

CC0 1.0 has its own dedication and fallback terms. The Public Domain Mark 1.0 labels an existing
public domain status. A custom declaration, separate permission, reserved rights, and unknown
rights each have distinct kinds. These can carry a supplied statement and its source URL.
A missing rights declaration records `Unknown`. An explicit Creative Commons kind without a terms
URL remains incomplete with a missing version and canonical terms URL.

For a legacy record that only has an attribution string or copyright notice, copy the supplied
values to `CreditLine` and `CopyrightNotice`. Leave `License` unset unless the caller has
evidence of an actual declaration. A source from Creative Commons, an empty copyright notice,
and a credit such as "via Creative Commons" leave the rights unknown.
A supplied URL that fails validation or contradicts an explicitly declared standard tool
returns a field error. Correct the input or record the unresolved wording as unknown.

## Prepare the public projection

Pass only reviewed public credit fields to this library. Keep proof of purchase, private
consent, contact information and publication approvals in the caller's records.
Review public URLs before rendering, especially query strings that may contain access tokens.
The projection exposes supplied fields for use in a caption, credits page or export. Consumers
choose what to publish and encode plain text and link attributes for the target format.
Validation bounds text to 1,024 UTF-16 code units per field, credit and prior notices to 20 entries
each, checks single-line printable text, and reports errors without echoing raw input.

Review the exact terms for the intended use. Creative Commons provides separate guidance for
commercial use, adapting or sharing adaptations, ShareAlike, and rights in third-party material.
A supported tool identifies the recorded terms. Your application owns checks for permission,
license compatibility, downstream obligations, publication and storage.
Carry forward any supplied warranty-disclaimer notice with the exact public credit wording.

- [Creative Commons license families](https://creativecommons.org/cc-licenses/)
- [Creative Commons Attribution 4.0 attribution conditions](https://creativecommons.org/licenses/by/4.0/legalcode.en#s3a)
- [Creative Commons public domain tools](https://creativecommons.org/public-domain/)
- [Creative Commons recommended attribution practices](https://wiki.creativecommons.org/wiki/Recommended_practices_for_attribution)
- [Creative Commons considerations for licensors and licensees](https://creativecommons.org/share-your-work/licensing-considerations/version4/)

## Existing image credits

Applications that store BuildingBlocks' [`ImageCredit`](../SharedKernel.BuildingBlocks/README.md#image-credits)
can turn it into the same reviewed public credit used by the gallery and the JSON-LD adapter.

```csharp
var recorded = ImageCredit.Create(
    "Ana Silva", "Serra Azul", "https://archive.example.org/serra",
    "CC BY 4.0", "https://creativecommons.org/licenses/by/4.0/", modified: true);

if (recorded.TryGetValue(out var imageCredit))
{
    var attribution = ContentAttribution.FromImageCredit(
        imageCredit,
        ContentLicenseKind.CcBy,
        modificationNotice: "Cropped for this gallery.",
        creatorKind: ContentCreditEntityKind.Person);
}
```

Pass the terms family established by the application's review. Use `Unknown` when only the
recorded label and address are available. Unknown records preserve that information as descriptive
wording in a full credit. Publication of a terms link or machine-readable `license` depends on a
reviewed declaration. `ImageLicense.KnownUrl` provides an editor lookup for common terms codes.
The projection uses the terms address already on the image credit. Supply the public wording for
a recorded modification. Supply the creator kind only when the review established it. Use
`ContentAttributionDefinition` directly when the source supplies other parties, notices or earlier
changes.

`ContentAttribution.FromImageCreditWithKnownTerms` can resolve a recorded
standard code to its official terms address. It prefers a supplied official
address when the family and version match, including a supported jurisdiction.
`DeclaredTermsUrl` preserves the original evidence. Unknown and conflicting
declarations retain their wording and address with an unresolved kind. The
application reviews publication and the source evidence for the exact image.
