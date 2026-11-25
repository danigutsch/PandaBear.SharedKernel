using SharedKernel.ContentRights;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Shared photo rights inputs for standalone and gallery rendering tests.</summary>
internal static class PhotoCreditFixture
{
    /// <summary>The localized labels used by the Portuguese photo credit fixtures.</summary>
    public static PhotoCreditLabels Labels { get; } = new(
        "Fotógrafo", "Titular", "Licenciante", "Crédito adicional", "Fonte original",
        "Termos", "Crédito completo", "Alterações", "Alterações anteriores");

    /// <summary>Makes a review-approved credit with a supplied rights declaration and locale-specific test notices.</summary>
    /// <param name="id">The photo's stable fragment id.</param>
    /// <param name="title">The photo label.</param>
    /// <param name="kind">The supplied declaration kind.</param>
    /// <param name="termsLabel">The optional terms label and original wording.</param>
    /// <param name="termsUrl">The optional absolute terms URL.</param>
    /// <returns>The gallery's public credit for the photo.</returns>
    public static PhotoCreditItem Photo(string id, string title, ContentLicenseKind kind, string? termsLabel = null, string? termsUrl = null)
    {
        var license = ContentLicense.Create(kind, termsLabel, termsUrl).ShouldBeSuccess();
        var credit = ContentAttribution.Create(new ContentAttributionDefinition
        {
            Title = title,
            Parties =
            [
                new ContentCreditParty("Ana <Silva>", ContentCreditRole.Creator, ContentCreditEntityKind.Person),
                new ContentCreditParty("Arquivo", ContentCreditRole.CopyrightOwner),
                new ContentCreditParty("Biblioteca", ContentCreditRole.Licensor),
                new ContentCreditParty("Acervo público", ContentCreditRole.Designated),
            ],
            OriginalSourceUrl = $"https://archive.example/photos/{id}",
            CreditLine = "Foto: Ana <Silva>.",
            CopyrightNotice = "© Arquivo",
            ModificationNotice = "Contraste ajustado",
            PreviousModificationNotices = ["Corte anterior"],
            License = license,
        }).ShouldBeSuccess().ToPublicCredit();

        return new PhotoCreditItem(id, title, credit) { TermsLabel = termsLabel };
    }
}
