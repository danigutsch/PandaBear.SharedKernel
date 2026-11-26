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
    [InlineData("CC BY 4.0", ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/4.0/")]
    [InlineData("CC BY-SA 3.0", ContentLicenseKind.CcBySa, "https://creativecommons.org/licenses/by-sa/3.0/")]
    [InlineData("CC BY-ND 2.5", ContentLicenseKind.CcByNd, "https://creativecommons.org/licenses/by-nd/2.5/")]
    [InlineData("CC BY-NC 2.0", ContentLicenseKind.CcByNc, "https://creativecommons.org/licenses/by-nc/2.0/")]
    [InlineData("CC BY-NC-SA 4.0", ContentLicenseKind.CcByNcSa, "https://creativecommons.org/licenses/by-nc-sa/4.0/")]
    [InlineData("CC BY-NC-ND 4.0", ContentLicenseKind.CcByNcNd, "https://creativecommons.org/licenses/by-nc-nd/4.0/")]
    [InlineData("CC0 1.0", ContentLicenseKind.Cc0, "https://creativecommons.org/publicdomain/zero/1.0/")]
    [InlineData("Public Domain Mark 1.0", ContentLicenseKind.PublicDomainMark, "https://creativecommons.org/publicdomain/mark/1.0/")]
    [InlineData("PDM 1.0", ContentLicenseKind.PublicDomainMark, "https://creativecommons.org/publicdomain/mark/1.0/")]
    public void Opt_in_resolves_the_official_terms_for_known_image_codes(
        string code, ContentLicenseKind kind, string terms)
    {
        // Arrange
        var legacy = ImageCredit.Create("Archive account", "Serra Azul", null, code, null, modified: false).ShouldBeSuccess();

        // Act
        var credit = ContentAttribution.FromImageCreditWithKnownTerms(legacy).ShouldBeSuccess().ToPublicCredit();

        // Assert
        credit.License.Kind.ShouldBe(kind);
        credit.License.DeclaredTerms.ShouldBe(code);
        credit.License.DeclaredTermsUrl.ShouldBeNull();
        credit.License.TermsUrl.ShouldNotBeNull().Value.ShouldBe(terms);
        credit.License.CanonicalTermsUrl.ShouldNotBeNull().Value.ShouldBe(terms);
    }

    [Fact]
    public void Opt_in_preserves_the_supplied_deed_of_a_matching_port()
    {
        // Arrange
        const string source = "https://archive.example.org/photos/serra";
        const string terms = "https://creativecommons.org/licenses/by-sa/3.0/br/deed.pt";
        const string change = "Cropped for the gallery";
        var legacy = ImageCredit.Create("Archive account", "Serra Azul", source, "CC BY-SA 3.0", terms, modified: true).ShouldBeSuccess();

        // Act
        var credit = ContentAttribution.FromImageCreditWithKnownTerms(legacy, change).ShouldBeSuccess().ToPublicCredit();

        // Assert
        credit.OriginalSourceUrl.ShouldNotBeNull().Value.ShouldBe(source);
        credit.ModificationNotice.ShouldBe(change);
        credit.License.Kind.ShouldBe(ContentLicenseKind.CcBySa);
        credit.License.TermsUrl.ShouldNotBeNull().Value.ShouldBe(terms);
        credit.License.CanonicalTermsUrl.ShouldNotBeNull().Value.ShouldBe("https://creativecommons.org/licenses/by-sa/3.0/br/");
        credit.License.Jurisdiction.ShouldBe("br");
    }

    [Theory]
    [InlineData("Archive terms", "https://archive.example.org/reuse")]
    [InlineData("CC BY 9.0", null)]
    [InlineData("CC BY 4.0", "https://creativecommons.org/licenses/by-sa/4.0/")]
    [InlineData("CC BY 4.0", "https://creativecommons.org/licenses/by/3.0/")]
    public void Opt_in_preserves_unknown_or_conflicting_terms_as_unresolved(string code, string? terms)
    {
        // Arrange
        var legacy = ImageCredit.Create("Archive account", null, null, code, terms, modified: false).ShouldBeSuccess();

        // Act
        var credit = ContentAttribution.FromImageCreditWithKnownTerms(legacy).ShouldBeSuccess().ToPublicCredit();

        // Assert
        credit.License.Kind.ShouldBe(ContentLicenseKind.Unknown);
        credit.License.DeclaredTerms.ShouldBe(code);
        credit.License.DeclaredTermsUrl.ShouldBe(terms);
        credit.License.CanonicalTermsUrl.ShouldBeNull();
    }

    [Fact]
    public void Opt_in_revalidates_a_restored_image_terms_URL()
    {
        // Arrange
        var legacy = ImageCredit.Restore("Archive account", null, null, ImageLicense.Restore("CC BY 4.0", "x"), modified: false);

        // Act
        var result = ContentAttribution.FromImageCreditWithKnownTerms(legacy);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().ContainsKey("termsUrl").ShouldBeTrue();
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
