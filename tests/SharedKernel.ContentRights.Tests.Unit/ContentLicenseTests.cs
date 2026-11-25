namespace PandaBear.ContentRights.Tests;

/// <summary>Exercises the official tool identification and declaration boundaries.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "ContentRights")]
public sealed class ContentLicenseTests
{
    [Theory]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/4.0/", "https://creativecommons.org/licenses/by/4.0/", "4.0", null)]
    [InlineData(ContentLicenseKind.CcBySa, "HTTP://CreativeCommons.ORG/licenses/by-sa/3.0/de/deed.de?ref=source", "https://creativecommons.org/licenses/by-sa/3.0/de/", "3.0", "de")]
    [InlineData(ContentLicenseKind.CcByNd, "https://creativecommons.org/licenses/by-nd/2.0/", "https://creativecommons.org/licenses/by-nd/2.0/", "2.0", null)]
    [InlineData(ContentLicenseKind.CcByNc, "https://creativecommons.org/licenses/by-nc/2.5/", "https://creativecommons.org/licenses/by-nc/2.5/", "2.5", null)]
    [InlineData(ContentLicenseKind.CcByNcSa, "https://creativecommons.org/licenses/by-nc-sa/2.1/jp/legalcode.ja", "https://creativecommons.org/licenses/by-nc-sa/2.1/jp/", "2.1", "jp")]
    [InlineData(ContentLicenseKind.CcByNcNd, "https://creativecommons.org/licenses/by-nc-nd/4.0/", "https://creativecommons.org/licenses/by-nc-nd/4.0/", "4.0", null)]
    [InlineData(ContentLicenseKind.CcByNcNd, "https://creativecommons.org/licenses/by-nd-nc/1.0/", "https://creativecommons.org/licenses/by-nd-nc/1.0/", "1.0", null)]
    [InlineData(ContentLicenseKind.Cc0, "https://creativecommons.org/publicdomain/zero/1.0/legalcode", "https://creativecommons.org/publicdomain/zero/1.0/", "1.0", null)]
    [InlineData(ContentLicenseKind.PublicDomainMark, "https://creativecommons.org/publicdomain/mark/1.0/deed.en", "https://creativecommons.org/publicdomain/mark/1.0/", "1.0", null)]
    public void Official_terms_preserve_the_recorded_URL_and_resolve_the_same_legal_tool(
        ContentLicenseKind kind, string supplied, string canonical, string version, string? jurisdiction)
    {
        // Arrange
        const string declaration = "Supplied wording © Studio";

        // Act
        var license = ContentLicense.Create(kind, declaration, supplied).ShouldBeSuccess();

        // Assert
        license.DeclaredTerms.ShouldBe(declaration);
        license.DeclaredTermsUrl.ShouldBe(supplied);
        license.CanonicalTermsUrl.ShouldNotBeNull().Value.ShouldBe(canonical);
        license.Version.ShouldBe(version);
        license.Jurisdiction.ShouldBe(jurisdiction);
        license.Kind.ShouldBe(kind);
    }

    [Theory]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by-sa/4.0/")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/4.0/de/")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/4.0//")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/4.0//deed.en")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/2.1/")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/9.0/")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/3.0/zz/")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/1.0/de/")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/4.0/unrecognized")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org.example.org/licenses/by/4.0/")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/4.0/../by-sa/4.0/")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/4.0/deed.unrecognized")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org?lookalike=/licenses/by/4.0/")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/3.0/de/legalcode.de/extra")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/3.0/de/extra")]
    [InlineData(ContentLicenseKind.CcBy, "https://creativecommons.org/licenses/by/3.0/de/deed.en--US")]
    [InlineData(ContentLicenseKind.PublicDomainMark, "https://creativecommons.org/publicdomain/mark/1.0/legalcode")]
    [InlineData(ContentLicenseKind.Cc0, "https://creativecommons.org/publicdomain/mark/1.0/")]
    [InlineData(ContentLicenseKind.PublicDomainMark, "https://creativecommons.org/publicdomain/zero/1.0/")]
    public void A_declared_standard_tool_requires_compatible_official_terms(
        ContentLicenseKind kind, string supplied)
    {
        // Act
        var rejected = ContentLicense.Create(kind, termsUrl: supplied);

        // Assert
        rejected.ErrorDetails.ShouldHaveValidationError("termsUrl", "Terms URL must identify the declared Creative Commons tool.");
    }

    [Theory]
    [InlineData("javascript:alert(0)")]
    [InlineData("https://private-token@creativecommons.org/licenses/by/4.0/")]
    [InlineData("//example.org/photos/")]
    [InlineData("https://example.org/photo link")]
    public void Unsafe_terms_URL_values_fail_validation_without_echoing_the_value(string supplied)
    {
        // Act
        var rejected = ContentLicense.Create(ContentLicenseKind.CustomTerms, termsUrl: supplied);

        // Assert
        rejected.IsFailure.ShouldBeTrue();
        rejected.ErrorDetails.ShouldNotBeNull().Detail.ShouldNotContain(supplied, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ContentLicenseKind.Unknown)]
    [InlineData(ContentLicenseKind.ReservedRights)]
    [InlineData(ContentLicenseKind.CustomTerms)]
    [InlineData(ContentLicenseKind.Permission)]
    public void Terms_provenance_alone_does_not_assign_a_standard_license(ContentLicenseKind kind)
    {
        // Arrange
        const string supplied = "https://creativecommons.org/licenses/by/4.0/";

        // Act
        var license = ContentLicense.Create(kind, "Use negotiated separately", supplied).ShouldBeSuccess();

        // Assert
        license.Kind.ShouldBe(kind);
        license.TermsUrl.ShouldNotBeNull().Value.ShouldBe(supplied);
        license.CanonicalTermsUrl.ShouldBeNull();
        license.Version.ShouldBeNull();
        license.Jurisdiction.ShouldBeNull();
    }

    [Fact]
    public void An_incomplete_declaration_preserves_the_supplied_kind_without_assuming_a_version()
    {
        // Act
        var license = ContentLicense.Create(ContentLicenseKind.CcBy, "CC BY").ShouldBeSuccess();

        // Assert
        license.Kind.ShouldBe(ContentLicenseKind.CcBy);
        license.DeclaredTerms.ShouldBe("CC BY");
        license.TermsUrl.ShouldBeNull();
        license.CanonicalTermsUrl.ShouldBeNull();
        license.Version.ShouldBeNull();
        license.Jurisdiction.ShouldBeNull();
    }

    [Fact]
    public void An_undefined_license_kind_is_rejected()
    {
        // Act
        var rejected = ContentLicense.Create((ContentLicenseKind)999);

        // Assert
        rejected.ErrorDetails.ShouldHaveValidationError("kind", "License kind is not defined.");
    }
}
