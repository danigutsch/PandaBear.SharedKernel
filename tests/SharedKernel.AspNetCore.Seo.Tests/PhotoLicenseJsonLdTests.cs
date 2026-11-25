using System.Text.Json;
using SharedKernel.BuildingBlocks;
using SharedKernel.ContentRights;

namespace SharedKernel.AspNetCore.Seo.Tests;

/// <summary>Exercises structured data for caller-approved photo credits.</summary>
public sealed class PhotoLicenseJsonLdTests
{
    [Theory]
    [InlineData(ContentLicenseKind.CcBy, true)]
    [InlineData(ContentLicenseKind.Unknown, false)]
    public void Projects_the_reviewed_legacy_image_credit_without_promoting_unknown_terms(ContentLicenseKind reviewedKind, bool includesLicense)
    {
        // Arrange
        const string terms = "https://creativecommons.org/licenses/by/4.0/";
        var image = WebUrl.CreateHttps("https://images.example.org/gallery/serra.webp").Value;
        var recorded = ImageCredit.Create("Ana <Silva>", "Serra Azul", "https://archive.example.org/serra",
            "CC BY 4.0", terms, modified: true).Value;
        var credit = ContentAttribution.FromImageCredit(recorded, reviewedKind,
            "Cropped for the gallery", ContentCreditEntityKind.Person).Value.ToPublicCredit();

        // Act
        var json = PhotoLicenseJsonLd.Serialize(image, credit);
        using var parsed = JsonDocument.Parse(json.ShouldNotBeNull());
        var metadata = parsed.RootElement;
        var creator = metadata.GetProperty("creator").EnumerateArray().ShouldHaveSingleItem();
        var hasLicense = metadata.TryGetProperty("license", out var license);
        var publishedTerms = hasLicense ? license.GetString() : null;
        var expectedTerms = includesLicense ? terms : null;

        // Assert
        creator.GetProperty("name").GetString().ShouldBe(recorded.Author);
        metadata.GetProperty("contentUrl").GetString().ShouldBe(image.Value);
        hasLicense.ShouldBe(includesLicense);
        publishedTerms.ShouldBe(expectedTerms);
        json.ShouldNotContain("https://archive.example.org/serra", StringComparison.Ordinal);
    }

    [Fact]
    public void Omits_metadata_for_an_unpublished_photo_even_when_credit_is_available()
    {
        // Arrange
        var credit = ContentAttribution.Create(new ContentAttributionDefinition
        {
            CreditLine = "Courtesy of the local archive",
            OriginalSourceUrl = "https://source.example.org/photo/123",
        }).Value.ToPublicCredit();

        // Act
        var json = PhotoLicenseJsonLd.Serialize(null, credit);

        // Assert
        json.ShouldBeNull();
    }

    [Fact]
    public void Omits_metadata_when_no_public_credit_is_available()
    {
        // Arrange
        var image = WebUrl.CreateHttps("https://images.example.org/published/market.jpg").Value;

        // Act
        var json = PhotoLicenseJsonLd.Serialize(image, null);

        // Assert
        json.ShouldBeNull();
    }

    [Fact]
    public void Publishes_the_same_credit_and_terms_as_the_visible_photo_credit()
    {
        // Arrange
        var image = WebUrl.CreateHttps("https://cdn.example.org/gallery/market.webp").Value;
        var request = WebUrl.CreateHttps("https://example.org/credits/market#requests").Value;
        var credit = ContentAttribution.Create(new ContentAttributionDefinition
        {
            Title = "Mercado público",
            Parties =
            [
                new ContentCreditParty("Ana Costa", ContentCreditRole.Creator, ContentCreditEntityKind.Person),
                new ContentCreditParty("Arquivo Municipal", ContentCreditRole.Creator, ContentCreditEntityKind.Organization),
                new ContentCreditParty("Acervo particular", ContentCreditRole.CopyrightOwner, ContentCreditEntityKind.Organization),
                new ContentCreditParty("North Photo", ContentCreditRole.Creator),
            ],
            OriginalSourceUrl = "https://archive.example.org/work/45",
            CreditLine = "Ana Costa and Arquivo Municipal",
            CopyrightNotice = "© 2024 Arquivo Municipal",
            ModificationNotice = "Cropped for the gallery",
            License = ContentLicense.Create(ContentLicenseKind.CcBy, "CC BY 4.0", "https://creativecommons.org/licenses/by/4.0/").Value,
        }).Value.ToPublicCredit();

        // Act
        var json = PhotoLicenseJsonLd.Serialize(image, credit, request);
        using var doc = JsonDocument.Parse(json.ShouldNotBeNull());
        var root = doc.RootElement;
        var creators = root.GetProperty("creator").EnumerateArray().ToArray();

        // Assert
        root.GetProperty("@context").GetString().ShouldBe("https://schema.org");
        root.GetProperty("@type").GetString().ShouldBe("ImageObject");
        root.GetProperty("contentUrl").GetString().ShouldBe(image.Value);
        root.GetProperty("creditText").GetString().ShouldBe(credit.CreditLine);
        root.GetProperty("copyrightNotice").GetString().ShouldBe(credit.CopyrightNotice);
        root.GetProperty("license").GetString().ShouldBe("https://creativecommons.org/licenses/by/4.0/");
        root.GetProperty("acquireLicensePage").GetString().ShouldBe(request.Value);
        creators.Select(creator => creator.GetProperty("@type").GetString()).ShouldBe(["Person", "Organization"]);
        creators.Select(creator => creator.GetProperty("name").GetString()).ShouldBe(["Ana Costa", "Arquivo Municipal"]);
        json.ShouldNotContain("https://archive.example.org/", StringComparison.Ordinal);
        json.ShouldNotContain("Cropped for the gallery", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ContentLicenseKind.Unknown, null)]
    [InlineData(ContentLicenseKind.ReservedRights, null)]
    [InlineData(ContentLicenseKind.Permission, null)]
    [InlineData(ContentLicenseKind.PublicDomainMark, "https://creativecommons.org/publicdomain/mark/1.0/")]
    [InlineData(ContentLicenseKind.CcBy, null)]
    public void Avoids_a_license_claim_when_no_public_license_terms_are_known(ContentLicenseKind kind, string? terms)
    {
        // Arrange
        var image = WebUrl.CreateHttps("https://images.example.org/gallery/item.webp").Value;
        var credit = ContentAttribution.Create(new ContentAttributionDefinition
        {
            CreditLine = "Archive photo",
            License = ContentLicense.Create(kind, termsUrl: terms).Value,
        }).Value.ToPublicCredit();

        // Act
        var json = PhotoLicenseJsonLd.Serialize(image, credit);
        using var doc = JsonDocument.Parse(json.ShouldNotBeNull());
        var properties = doc.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        // Assert
        properties.ShouldBe(["@context", "@type", "contentUrl", "creditText"]);
    }

    [Theory]
    [InlineData(ContentLicenseKind.CustomTerms, "https://archive.example.org/terms/v2")]
    [InlineData(ContentLicenseKind.Cc0, "https://creativecommons.org/publicdomain/zero/1.0/")]
    public void Publishes_explicit_custom_terms_or_a_public_domain_dedication(ContentLicenseKind kind, string terms)
    {
        // Arrange
        var image = WebUrl.CreateHttps("https://images.example.org/published/item.jpeg").Value;
        var credit = ContentAttribution.Create(new ContentAttributionDefinition
        {
            License = ContentLicense.Create(kind, termsUrl: terms).Value,
        }).Value.ToPublicCredit();

        // Act
        var json = PhotoLicenseJsonLd.Serialize(image, credit);
        using var doc = JsonDocument.Parse(json.ShouldNotBeNull());

        // Assert
        doc.RootElement.GetProperty("license").GetString().ShouldBe(terms);
    }

    [Fact]
    public void Keeps_a_license_request_page_distinct_from_the_license_terms()
    {
        // Arrange
        var image = WebUrl.CreateHttps("https://images.example.org/published/archive-photo.webp").Value;
        var request = WebUrl.CreateHttps("https://archive.example.org/permissions/photo").Value;
        var credit = ContentAttribution.Create(new ContentAttributionDefinition { CreditLine = "City archive" }).Value.ToPublicCredit();

        // Act
        var json = PhotoLicenseJsonLd.Serialize(image, credit, request);
        using var doc = JsonDocument.Parse(json.ShouldNotBeNull());
        var root = doc.RootElement;
        var hasLicense = root.TryGetProperty("license", out _);

        // Assert
        root.GetProperty("acquireLicensePage").GetString().ShouldBe(request.Value);
        hasLicense.ShouldBeFalse();
    }

    [Fact]
    public void Encodes_HTML_delimiters_in_the_JSON_script_value()
    {
        // Arrange
        const string creditLine = "História urbana </script> & arquivo";
        var image = WebUrl.CreateHttps("https://images.example.org/published/item.webp?size=wide&density=2").Value;
        var credit = ContentAttribution.Create(new ContentAttributionDefinition { CreditLine = creditLine }).Value.ToPublicCredit();

        // Act
        var json = PhotoLicenseJsonLd.Serialize(image, credit);
        using var doc = JsonDocument.Parse(json.ShouldNotBeNull());

        // Assert
        json.ShouldNotContain("<", StringComparison.Ordinal);
        json.ShouldNotContain(">", StringComparison.Ordinal);
        json.ShouldNotContain("&", StringComparison.Ordinal);
        doc.RootElement.GetProperty("creditText").GetString().ShouldBe(creditLine);
        doc.RootElement.GetProperty("contentUrl").GetString().ShouldBe(image.Value);
    }
}
