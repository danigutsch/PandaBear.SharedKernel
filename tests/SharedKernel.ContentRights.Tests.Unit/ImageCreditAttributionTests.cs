namespace SharedKernel.ContentRights.Tests;

/// <summary>Checks conversion from the published image credit contract to a reviewed public credit.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "ContentRights")]
public sealed class ImageCreditAttributionTests
{
    [Fact]
    public void Preserves_a_reviewed_image_credit_and_its_change_notice()
    {
        // Arrange
        const string source = "https://archive.example.org/photos/serra";
        const string terms = "https://creativecommons.org/licenses/by/4.0/legalcode.en";
        var legacy = ImageCredit.Create("Ana Silva", "Serra Azul", source, "CC BY 4.0", terms, modified: true).ShouldBeSuccess();

        // Act
        var imported = ContentAttribution.FromImageCredit(legacy, ContentLicenseKind.CcBy,
            "Cropped for this gallery", ContentCreditEntityKind.Person).ShouldBeSuccess();
        var credit = imported.ToPublicCredit();

        // Assert
        imported.DeclaredOriginalSourceUrl.ShouldBe(source);
        credit.Title.ShouldBe(legacy.Title);
        credit.Parties.ShouldBe([new ContentCreditParty(legacy.Author, ContentCreditRole.Creator, ContentCreditEntityKind.Person)]);
        credit.OriginalSourceUrl.ShouldNotBeNull().Value.ShouldBe(source);
        credit.License.DeclaredTerms.ShouldBe(legacy.License.Code);
        credit.License.DeclaredTermsUrl.ShouldBe(terms);
        credit.License.CanonicalTermsUrl.ShouldNotBeNull().Value.ShouldBe("https://creativecommons.org/licenses/by/4.0/");
        credit.ModificationNotice.ShouldBe("Cropped for this gallery");
    }

    [Fact]
    public void Retains_legacy_terms_without_resolving_an_unreviewed_license()
    {
        // Arrange
        const string terms = "https://creativecommons.org/licenses/by/4.0/";
        var legacy = ImageCredit.Create("Archive account", null, null, "CC BY 4.0", terms, modified: false).ShouldBeSuccess();

        // Act
        var credit = ContentAttribution.FromImageCredit(legacy, ContentLicenseKind.Unknown).ShouldBeSuccess().ToPublicCredit();

        // Assert
        credit.Parties.ShouldBe([new ContentCreditParty(legacy.Author, ContentCreditRole.Creator)]);
        credit.License.Kind.ShouldBe(ContentLicenseKind.Unknown);
        credit.License.DeclaredTerms.ShouldBe(legacy.License.Code);
        credit.License.DeclaredTermsUrl.ShouldBe(terms);
        credit.License.CanonicalTermsUrl.ShouldBeNull();
        credit.ModificationNotice.ShouldBeNull();
    }

    [Fact]
    public void Does_not_suggest_a_terms_URL_from_the_display_code()
    {
        // Arrange
        var legacy = ImageCredit.Create("Archive account", null, null, "CC BY 4.0", null, modified: false).ShouldBeSuccess();

        // Act
        var credit = ContentAttribution.FromImageCredit(legacy, ContentLicenseKind.CcBy).ShouldBeSuccess().ToPublicCredit();

        // Assert
        credit.License.Kind.ShouldBe(ContentLicenseKind.CcBy);
        credit.License.TermsUrl.ShouldBeNull();
        credit.License.CanonicalTermsUrl.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void A_recorded_change_needs_public_wording(string? notice)
    {
        // Arrange
        var legacy = ImageCredit.Create("Archive account", null, null, "Archive terms", null, modified: true).ShouldBeSuccess();

        // Act
        var result = ContentAttribution.FromImageCredit(legacy, ContentLicenseKind.Unknown, notice);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().ContainsKey("modificationNotice").ShouldBeTrue();
    }

    [Fact]
    public void Reports_terms_that_do_not_identify_the_reviewed_license()
    {
        // Arrange
        var legacy = ImageCredit.Create("Archive account", null, null, "CC BY 4.0",
            "https://creativecommons.org/licenses/by-sa/4.0/", modified: false).ShouldBeSuccess();

        // Act
        var result = ContentAttribution.FromImageCredit(legacy, ContentLicenseKind.CcBy);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().ContainsKey("termsUrl").ShouldBeTrue();
    }

    [Fact]
    public void Revalidates_the_source_of_a_restored_image_credit()
    {
        // Arrange
        var legacy = ImageCredit.Restore("Archive account", null, "invalid source",
            ImageLicense.Restore("Archive terms", null), modified: false);

        // Act
        var result = ContentAttribution.FromImageCredit(legacy, ContentLicenseKind.Unknown);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().ContainsKey("originalSourceUrl").ShouldBeTrue();
    }

    [Fact]
    public void Requires_an_existing_image_credit()
    {
        // Arrange
        Action convert = () => ContentAttribution.FromImageCredit(null!, ContentLicenseKind.Unknown);

        // Act & Assert
        convert.ShouldThrow<ArgumentNullException>();
    }
}
