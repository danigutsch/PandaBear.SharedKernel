using SharedKernel.BuildingBlocks;
using SharedKernel.ContentRights;
using static SharedKernel.Blazor.Site.Tests.PhotoCreditFixture;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests reusable compact photo credits and the standalone photo credits page.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class PhotoCreditTests : BunitContext
{
    [Theory]
    [InlineData(ContentLicenseKind.CcBy, 1)]
    [InlineData(ContentLicenseKind.Unknown, 0)]
    public void Imported_image_credits_preserve_creator_source_and_changes_with_reviewed_terms_links(
        ContentLicenseKind reviewedKind, int termsLinkCount)
    {
        // Arrange
        const string source = "https://archive.example.org/serra";
        const string terms = "https://creativecommons.org/licenses/by/4.0/";
        const string modification = "Cores ajustadas";
        var recorded = ImageCredit.Create("Ana <Silva>", "Serra Azul", source, "CC BY 4.0", terms, modified: true).Value;
        var credit = ContentAttribution.FromImageCredit(recorded, reviewedKind,
            modification, ContentCreditEntityKind.Person).Value.ToPublicCredit();
        var item = new PhotoCreditItem("serra", "Serra Azul", credit);

        // Act
        var cut = Render<PhotoCredit>(parameters => parameters
            .Add(component => component.Item, item)
            .Add(component => component.Labels, Labels));

        // Assert
        cut.Find(".site-photo-credit").TextContent.ShouldContain(recorded.Author, StringComparison.Ordinal);
        cut.Find(".site-photo-credit").TextContent.ShouldContain(modification, StringComparison.Ordinal);
        cut.Find(".site-photo-credit").TextContent.ShouldContain(recorded.License.Code, StringComparison.Ordinal);
        cut.FindAll($"a[href='{source}']").Count.ShouldBe(1);
        cut.FindAll($"a[href='{terms}']").Count.ShouldBe(termsLinkCount);
    }

    [Fact]
    public void Full_credit_preserves_requested_credit_and_public_provenance()
    {
        // Arrange
        var item = Photo("cascata", "Cascata & névoa", ContentLicenseKind.CcBy, "CC BY 4.0",
            "https://creativecommons.org/licenses/by/4.0/legalcode.en");

        // Act
        var cut = Render<PhotoCredit>(parameters => parameters
            .Add(component => component.Item, item)
            .Add(component => component.Labels, Labels));

        // Assert
        cut.Find(".site-photo-credit").TextContent.ShouldContain("Foto: Ana <Silva>.", StringComparison.Ordinal);
        cut.FindAll("silva").Count.ShouldBe(0);
        cut.Find(".site-photo-credit").TextContent.ShouldContain("© Arquivo", StringComparison.Ordinal);
        cut.FindAll("dt").Select(term => term.TextContent).ShouldBeEquivalentTo(
            "Fotógrafo", "Titular", "Licenciante", "Crédito adicional", "Fonte original", "Termos", "Alterações", "Alterações anteriores");
        cut.Find("a[href='https://archive.example/photos/cascata']").TextContent.ShouldContain("Fonte original", StringComparison.Ordinal);
        cut.Find("a[href='https://creativecommons.org/licenses/by/4.0/legalcode.en']").TextContent.ShouldContain("CC BY 4.0", StringComparison.Ordinal);
        cut.Find(".site-photo-credit").TextContent.ShouldContain("Corte anterior", StringComparison.Ordinal);
        cut.Find(".site-photo-credit").TextContent.ShouldContain("Contraste ajustado", StringComparison.Ordinal);
    }

    [Fact]
    public void Compact_credit_exposes_terms_and_a_descriptive_link_to_the_correct_photo()
    {
        // Arrange
        var item = Photo("cascata", "Cascata", ContentLicenseKind.CcBySa, "CC BY-SA 3.0",
            "https://creativecommons.org/licenses/by-sa/3.0/br/") with
        {
            DetailsHref = LinkTarget.FromString("/creditos#cascata"),
        };

        // Act
        var cut = Render<PhotoCredit>(parameters => parameters
            .Add(component => component.Item, item)
            .Add(component => component.Labels, Labels)
            .Add(component => component.Compact, true));

        // Assert
        cut.Find(".site-photo-credit__summary").TextContent.ShouldContain("Ana <Silva>", StringComparison.Ordinal);
        cut.Find("a[href='/creditos#cascata']").TextContent.ShouldContain("Crédito completo", StringComparison.Ordinal);
        cut.Find("a[href='/creditos#cascata'] .site-visually-hidden").TextContent.Trim().ShouldBe("Cascata");
        cut.Find("a[href='https://creativecommons.org/licenses/by-sa/3.0/br/']").TextContent.Trim().ShouldBe("CC BY-SA 3.0 Cascata");
        cut.FindAll("details").Count.ShouldBe(0);
    }

    [Fact]
    public void A_compact_credit_without_a_page_link_offers_native_expansion()
    {
        // Arrange
        var item = Photo("cascata", "Cascata", ContentLicenseKind.CustomTerms, "Termos do arquivo", "https://archive.example/terms");

        // Act
        var cut = Render<PhotoCredit>(parameters => parameters
            .Add(component => component.Item, item)
            .Add(component => component.Labels, Labels)
            .Add(component => component.Compact, true));

        // Assert
        cut.Find("details > summary").TextContent.ShouldContain("Crédito completo", StringComparison.Ordinal);
        cut.Find("details a[href='https://archive.example/photos/cascata']").TextContent.ShouldContain("Fonte original", StringComparison.Ordinal);
        cut.Find("details").HasAttribute("open").ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, ".site-photo-credit cite")]
    [InlineData(true, "details cite")]
    public void Full_credit_keeps_the_original_title_when_it_is_the_only_reviewed_field(bool compact, string selector)
    {
        // Arrange
        const string originalTitle = "Waterfall <Grande>";
        var credit = ContentAttribution.Create(new() { Title = originalTitle }).ShouldBeSuccess().ToPublicCredit();
        var item = new PhotoCreditItem("cascata", "Cascata", credit);

        // Act
        var cut = Render<PhotoCredit>(parameters => parameters
            .Add(component => component.Item, item)
            .Add(component => component.Labels, Labels)
            .Add(component => component.Compact, compact));

        // Assert
        cut.Find(selector).TextContent.ShouldBe(originalTitle);
        cut.FindAll(".site-photo-credit__summary cite, grande").Count.ShouldBe(0);
    }

    [Fact]
    public void An_empty_unknown_credit_renders_nothing_and_an_unknown_declaration_has_no_terms_link()
    {
        // Arrange
        var empty = new PhotoCreditItem("a", "A", ContentAttribution.Create(new()).ShouldBeSuccess().ToPublicCredit());
        var ambiguous = Photo("b", "B", ContentLicenseKind.Unknown, "Sem licença confirmada",
            "https://creativecommons.org/licenses/by/4.0/");

        // Act
        var noCredit = Render<PhotoCredit>(parameters => parameters
            .Add(component => component.Item, empty)
            .Add(component => component.Labels, Labels)
            .Add(component => component.Compact, true));
        var unknown = Render<PhotoCredit>(parameters => parameters
            .Add(component => component.Item, ambiguous)
            .Add(component => component.Labels, Labels));

        // Assert
        noCredit.Markup.Trim().ShouldBe(string.Empty);
        unknown.FindAll("a[href='https://creativecommons.org/licenses/by/4.0/']").Count.ShouldBe(0);
        unknown.Find(".site-photo-credit").TextContent.ShouldContain("Sem licença confirmada", StringComparison.Ordinal);
    }

    [Fact]
    public void A_credits_page_keeps_per_photo_anchors_and_mixed_license_details_in_order()
    {
        // Arrange
        var first = Photo("cascata", "The Original Waterfall", ContentLicenseKind.CcBy, "CC BY 4.0",
            "https://creativecommons.org/licenses/by/4.0/") with
        {
            Title = "Cascata",
        };
        var second = Photo("serra", "Serra", ContentLicenseKind.ReservedRights, "Direitos reservados");

        // Act
        var cut = Render<PhotoCreditsPage>(parameters => parameters
            .Add(component => component.Heading, "Créditos das fotos")
            .Add(component => component.Intro, "Estes créditos cobrem somente as fotos desta página.")
            .Add(component => component.HeadingId, "creditos")
            .Add(component => component.Items, [first, second])
            .Add(component => component.Labels, Labels));

        // Assert
        cut.Find("section[aria-labelledby='creditos'] h1").TextContent.ShouldBe("Créditos das fotos");
        cut.Find(".site-section__intro").TextContent.ShouldBe("Estes créditos cobrem somente as fotos desta página.");
        cut.FindAll("li.site-photo-credits__item").Select(element => element.Id).ShouldBeEquivalentTo("cascata", "serra");
        cut.Find("#cascata h2").TextContent.ShouldBe("Cascata");
        cut.Find("#cascata .site-photo-credit cite").TextContent.ShouldBe("The Original Waterfall");
        cut.Find("#serra").GetAttribute("tabindex").ShouldBe("-1");
        cut.Find("#cascata a[href='https://creativecommons.org/licenses/by/4.0/']").TextContent.ShouldContain("CC BY 4.0", StringComparison.Ordinal);
        cut.Find("#serra").TextContent.ShouldContain("Direitos reservados", StringComparison.Ordinal);
        cut.FindAll("#serra a[href*='creativecommons.org']").Count.ShouldBe(0);
    }

    [Fact]
    public void A_credits_page_links_opted_in_known_terms_and_keeps_reviewed_controls_unlinked()
    {
        // Arrange
        const string originalSource = "https://archive.example.org/photos/serra";
        const string canonicalTerms = "https://creativecommons.org/licenses/by/4.0/";
        const string modification = "Cores ajustadas";
        const string localizedTerms = "Licença CC BY 4.0";
        var recorded = ImageCredit.Create("Ana Silva", "Serra Azul", originalSource, "cc  bY  4.0", null, modified: true).ShouldBeSuccess();
        var unresolved = ImageCredit.Create("Arquivo", null, null, "CC BY 4.0", canonicalTerms, modified: false).ShouldBeSuccess();
        var recognized = ContentAttribution.FromImageCreditWithKnownTerms(recorded, modification).ShouldBeSuccess().ToPublicCredit();
        var withoutAddress = ContentAttribution.FromImageCredit(recorded, ContentLicenseKind.CcBy, modification).ShouldBeSuccess().ToPublicCredit();
        var unknown = ContentAttribution.FromImageCredit(unresolved, ContentLicenseKind.Unknown).ShouldBeSuccess().ToPublicCredit();
        PhotoCreditItem[] items =
        [
            new("recognized", "Serra Azul", recognized) { TermsLabel = localizedTerms },
            new("without-address", "Sem endereço", withoutAddress),
            new("unknown", "Sem revisão", unknown),
        ];

        // Act
        var cut = Render<PhotoCreditsPage>(parameters => parameters
            .Add(component => component.Heading, "Créditos das fotos")
            .Add(component => component.Items, items)
            .Add(component => component.Labels, Labels));

        // Assert
        var termsLink = cut.Find($"#recognized a[href='{canonicalTerms}']");
        termsLink.TextContent.Trim().ShouldBe("Licença CC BY 4.0 Serra Azul");
        cut.Find($"#recognized a[href='{originalSource}']").TextContent.Trim().ShouldBe("Fonte original Serra Azul");
        cut.Find("#recognized").TextContent.ShouldContain(modification, StringComparison.Ordinal);
        var withoutAddressLinks = cut.FindAll("#without-address a");
        withoutAddressLinks.Count.ShouldBe(1);
        withoutAddressLinks[0].GetAttribute("href").ShouldBe(originalSource);
        cut.Find("#without-address").TextContent.ShouldContain(recorded.License.Code, StringComparison.Ordinal);
        cut.FindAll("#unknown a").Count.ShouldBe(0);
        cut.Find("#unknown").TextContent.ShouldContain(unresolved.License.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void Repeated_anchors_are_rejected_before_rendering()
    {
        // Arrange
        var item = Photo("same", "A photo", ContentLicenseKind.Unknown);

        // Act
        var render = () => Render<PhotoCreditsPage>(parameters => parameters
            .Add(component => component.Heading, "Photo credits")
            .Add(component => component.Items, [item, item])
            .Add(component => component.Labels, Labels));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(PhotoCreditsPage.Items));
    }

    [Fact]
    public void A_credits_page_requires_a_bounded_window()
    {
        // Arrange
        var item = Photo("photo", "A photo", ContentLicenseKind.Unknown);
        var items = Enumerable.Repeat(item, PhotoCreditsPage.MaxItems + 1).ToArray();

        // Act
        var render = () => Render<PhotoCreditsPage>(parameters => parameters
            .Add(component => component.Heading, "Photo credits")
            .Add(component => component.Items, items)
            .Add(component => component.Labels, Labels));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(PhotoCreditsPage.Items));
    }

    [Theory]
    [InlineData("")]
    [InlineData("white space")]
    public void An_invalid_fragment_identifier_is_rejected(string id)
    {
        // Arrange
        var item = Photo("valid", "A photo", ContentLicenseKind.Unknown) with { Id = id };

        // Act
        var render = () => Render<PhotoCredit>(parameters => parameters
            .Add(component => component.Item, item)
            .Add(component => component.Labels, Labels));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(PhotoCredit.Item));
    }

    [Fact]
    public void Labels_for_link_and_disclosure_controls_must_be_present()
    {
        // Arrange
        var item = Photo("photo", "A photo", ContentLicenseKind.Unknown);
        var labels = Labels with { Details = " " };

        // Act
        var render = () => Render<PhotoCredit>(parameters => parameters
            .Add(component => component.Item, item)
            .Add(component => component.Labels, labels));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(PhotoCredit.Labels));
    }
}
