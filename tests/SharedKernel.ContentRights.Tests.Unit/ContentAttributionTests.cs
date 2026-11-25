namespace SharedKernel.ContentRights.Tests;

/// <summary>Checks the bounded public credit projection and provenance preservation.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "ContentRights")]
public sealed class ContentAttributionTests
{
    [Fact]
    public void Public_credit_preserves_licensed_and_adapted_work_details_and_owns_its_collections()
    {
        // Arrange
        var license = ContentLicense.Create(ContentLicenseKind.CcBy, "Creative Commons Attribution 4.0",
            "https://creativecommons.org/licenses/by/4.0/legalcode.en").ShouldBeSuccess();
        var creator = new ContentCreditParty("Alex Silva", ContentCreditRole.Creator, ContentCreditEntityKind.Person);
        var publisher = new ContentCreditParty("Studio", ContentCreditRole.CopyrightOwner, ContentCreditEntityKind.Organization);
        var licensor = new ContentCreditParty("Archive", ContentCreditRole.Licensor);
        var credit = new ContentCreditParty("City project", ContentCreditRole.Designated);
        var parties = new[] { creator, publisher, licensor, credit };
        var previous = new[] { "Cropped by Studio." };
        var definition = new ContentAttributionDefinition
        {
            Title = "Sunrise over Serra",
            Parties = parties,
            OriginalSourceUrl = "HTTPS://Images.Example.Org/photos/sunrise?photo=1",
            CreditLine = "Photo: Alex Silva / Studio",
            CopyrightNotice = "© Studio, 2024",
            ModificationNotice = "Color corrected for this edition.",
            PreviousModificationNotices = previous,
            License = license,
        };

        // Act
        var attribution = ContentAttribution.Create(definition).ShouldBeSuccess();
        parties[0] = publisher;
        previous[0] = "Changed in the caller.";
        var display = attribution.ToPublicCredit();

        // Assert
        attribution.DeclaredOriginalSourceUrl.ShouldBe(definition.OriginalSourceUrl);
        display.Title.ShouldBe(definition.Title);
        display.Parties.ShouldBe([creator, publisher, licensor, credit]);
        display.OriginalSourceUrl.ShouldNotBeNull().Value.ShouldBe("https://images.example.org/photos/sunrise?photo=1");
        display.CreditLine.ShouldBe(definition.CreditLine);
        display.CopyrightNotice.ShouldBe(definition.CopyrightNotice);
        display.ModificationNotice.ShouldBe(definition.ModificationNotice);
        display.PreviousModificationNotices.ShouldBe(["Cropped by Studio."]);
        display.License.ShouldBe(license);
    }

    [Fact]
    public void Legacy_free_text_and_sources_do_not_assign_new_rights()
    {
        // Arrange
        var definition = new ContentAttributionDefinition
        {
            CreditLine = "Original photo, Alex Silva ",
            CopyrightNotice = "© Alex Silva / 2024",
            OriginalSourceUrl = "https://creativecommons.org/gallery/image",
        };

        // Act
        var display = ContentAttribution.Create(definition).ShouldBeSuccess().ToPublicCredit();

        // Assert
        display.CreditLine.ShouldBe(definition.CreditLine);
        display.CopyrightNotice.ShouldBe(definition.CopyrightNotice);
        display.License.Kind.ShouldBe(ContentLicenseKind.Unknown);
        display.License.TermsUrl.ShouldBeNull();
        display.License.CanonicalTermsUrl.ShouldBeNull();
    }

    [Fact]
    public void An_empty_record_returns_explicit_unknown_rights_without_creating_credit_data()
    {
        // Act
        var display = ContentAttribution.Create(new ContentAttributionDefinition()).ShouldBeSuccess().ToPublicCredit();

        // Assert
        display.Title.ShouldBeNull();
        display.Parties.ShouldBeEmpty();
        display.OriginalSourceUrl.ShouldBeNull();
        display.CreditLine.ShouldBeNull();
        display.CopyrightNotice.ShouldBeNull();
        display.ModificationNotice.ShouldBeNull();
        display.PreviousModificationNotices.ShouldBeEmpty();
        display.License.Kind.ShouldBe(ContentLicenseKind.Unknown);
    }

    [Fact]
    public void Invalid_attribution_fields_are_reported_together()
    {
        // Arrange
        var definition = new ContentAttributionDefinition
        {
            CreditLine = "credit\u0000invalid",
            OriginalSourceUrl = "javascript:alert(0)",
            Parties = [new ContentCreditParty("", (ContentCreditRole)999, (ContentCreditEntityKind)999)],
            PreviousModificationNotices = [" "],
        };

        // Act
        var rejected = ContentAttribution.Create(definition);

        // Assert
        var validation = rejected.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        validation.Keys.ShouldContain("creditLine");
        validation.Keys.ShouldContain("originalSourceUrl");
        validation.Keys.ShouldContain("parties[0].name");
        validation.Keys.ShouldContain("parties[0].role");
        validation.Keys.ShouldContain("parties[0].entityKind");
        validation.Keys.ShouldContain("previousModificationNotices[0]");
    }

    [Fact]
    public void Text_and_collection_lengths_are_bounded_before_processing_items()
    {
        // Arrange
        var oversized = new string('x', ContentAttribution.MaxTextLength + 1);
        var definition = new ContentAttributionDefinition
        {
            Title = oversized,
            Parties = [.. Enumerable.Repeat(new ContentCreditParty("Alex", ContentCreditRole.Creator), ContentAttribution.MaxCreditParties + 1)],
            PreviousModificationNotices = [.. Enumerable.Repeat("Cropped.", ContentAttribution.MaxPreviousModificationNotices + 1)],
        };

        // Act
        var attribution = ContentAttribution.Create(definition);
        var license = ContentLicense.Create(ContentLicenseKind.CustomTerms, oversized);

        // Assert
        var validation = attribution.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        validation.Keys.ShouldContain("title");
        validation.Keys.ShouldContain("parties");
        validation.Keys.ShouldContain("previousModificationNotices");
        license.ErrorDetails.ShouldHaveValidationError("declaredTerms", "Declared terms exceeds the text length limit.");
    }

    [Fact]
    public void Private_evidence_can_be_retained_by_a_caller_separately_from_the_display_shape()
    {
        // Arrange
        var reviewed = new
        {
            Public = new ContentAttributionDefinition { CreditLine = "Forest Archive" },
            PrivateApprovalEvidence = "Internal reviewer, consent attachment path, restricted contact",
        };

        // Act
        var display = ContentAttribution.Create(reviewed.Public).ShouldBeSuccess().ToPublicCredit();

        // Assert
        display.CreditLine.ShouldBe("Forest Archive");
        var propertyNames = typeof(PublicContentCredit).GetProperties().Select(property => property.Name);
        propertyNames.Order(StringComparer.Ordinal).ToArray().ShouldBe(
            [
                nameof(PublicContentCredit.CopyrightNotice), nameof(PublicContentCredit.CreditLine),
                nameof(PublicContentCredit.License), nameof(PublicContentCredit.ModificationNotice),
                nameof(PublicContentCredit.OriginalSourceUrl), nameof(PublicContentCredit.Parties),
                nameof(PublicContentCredit.PreviousModificationNotices), nameof(PublicContentCredit.Title),
            ]);
        propertyNames.ShouldNotContain(nameof(reviewed.PrivateApprovalEvidence));
    }

    [Fact]
    public void Optional_blank_text_is_absent_and_null_collection_items_are_invalid()
    {
        // Arrange
        var blank = new ContentAttributionDefinition { Title = " ", CreditLine = "", ModificationNotice = " " };
        var invalid = new ContentAttributionDefinition { Parties = [null!], PreviousModificationNotices = [null!] };

        // Act
        var display = ContentAttribution.Create(blank).ShouldBeSuccess().ToPublicCredit();
        var rejected = ContentAttribution.Create(invalid);

        // Assert
        display.Title.ShouldBeNull();
        display.CreditLine.ShouldBeNull();
        display.ModificationNotice.ShouldBeNull();
        rejected.ErrorDetails.ShouldHaveValidationError("parties[0]", "Credit party is required.");
        rejected.ErrorDetails.ShouldHaveValidationError("previousModificationNotices[0]", "Previous modification notice is required.");
    }
}
