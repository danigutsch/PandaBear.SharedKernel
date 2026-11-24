using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the language tags that the cultures of a supported set declare.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "Localization")]
public sealed class CultureLanguageTagsTests
{
    private static readonly ContentCulture Pt = ContentCulture.Restore("pt");
    private static readonly ContentCulture En = ContentCulture.Restore("en");
    private static readonly ContentCulture Fr = ContentCulture.Restore("fr");
    private static readonly ContentCulture PtBr = ContentCulture.Restore("pt-BR");
    private static readonly SupportedCultures Supported = SupportedCultures.Create(Pt, [Pt, En]).ShouldBeSuccess();

    [Fact]
    public void A_mapped_culture_declares_its_language_tag_and_the_others_declare_themselves()
    {
        // Arrange
        var tags = CultureLanguageTags.Create(Supported, [new CultureLanguageTag(Pt, PtBr)]).ShouldBeSuccess();

        // Act
        var portuguese = tags.LanguageTagOf(Pt);
        var english = tags.LanguageTagOf(En);

        // Assert
        portuguese.ShouldBe(PtBr);
        english.ShouldBe(En);
    }

    [Fact]
    public void Lists_the_language_tags_in_the_order_of_the_cultures_with_the_default_culture_tag()
    {
        // Act
        var tags = CultureLanguageTags.Create(Supported, [new CultureLanguageTag(Pt, PtBr)]).ShouldBeSuccess();

        // Assert
        tags.Cultures.ShouldBe(Supported);
        tags.LanguageTags.ShouldBe([PtBr, En]);
        tags.DefaultLanguageTag.ShouldBe(PtBr);
    }

    [Fact]
    public void Without_mappings_every_culture_declares_itself()
    {
        // Act
        var tags = CultureLanguageTags.Create(Supported, []).ShouldBeSuccess();

        // Assert
        tags.LanguageTags.ShouldBe([Pt, En]);
        tags.DefaultLanguageTag.ShouldBe(Pt);
    }

    [Theory]
    [InlineData("pt-BR", "pt")]
    [InlineData("PT-br", "pt")]
    [InlineData("en", "en")]
    public void TryGetCulture_maps_a_language_tag_back_to_its_culture(string languageTag, string expected)
    {
        // Arrange
        var tags = CultureLanguageTags.Create(Supported, [new CultureLanguageTag(Pt, PtBr)]).ShouldBeSuccess();

        // Act
        var found = tags.TryGetCulture(languageTag, out var culture);

        // Assert
        found.ShouldBeTrue();
        culture.ShouldNotBeNull().Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("pt")]
    [InlineData("pt-PT")]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("../x")]
    [InlineData(null)]
    public void TryGetCulture_rejects_text_that_is_not_a_declared_language_tag(string? languageTag)
    {
        // Arrange
        var tags = CultureLanguageTags.Create(Supported, [new CultureLanguageTag(Pt, PtBr)]).ShouldBeSuccess();

        // Act
        var found = tags.TryGetCulture(languageTag, out var culture);

        // Assert
        found.ShouldBeFalse("a mapped culture's own name is no longer a declared language tag");
        culture.ShouldBeNull();
    }

    [Fact]
    public void TryGetCulture_maps_a_typed_language_tag_back_to_its_culture()
    {
        // Arrange
        var tags = CultureLanguageTags.Create(Supported, [new CultureLanguageTag(Pt, PtBr)]).ShouldBeSuccess();

        // Act
        var found = tags.TryGetCulture(PtBr, out var culture);
        var foundNone = tags.TryGetCulture((ContentCulture?)null, out var none);

        // Assert
        found.ShouldBeTrue();
        culture.ShouldBe(Pt);
        foundNone.ShouldBeFalse();
        none.ShouldBeNull();
    }

    [Fact]
    public void LanguageTagOf_rejects_a_culture_that_is_not_supported()
    {
        // Arrange
        var tags = CultureLanguageTags.Create(Supported, []).ShouldBeSuccess();
        var unsupported = () => { tags.LanguageTagOf(Fr); };
        var nullCulture = () => { tags.LanguageTagOf(null!); };

        // Act
        var exception = unsupported.ShouldThrow<ArgumentException>();
        var nullException = nullCulture.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("culture");
        nullException.ParamName.ShouldBe("culture");
    }

    [Fact]
    public void A_culture_that_is_not_supported_is_rejected()
    {
        // Act
        var result = CultureLanguageTags.Create(Supported, [new CultureLanguageTag(Fr, ContentCulture.Restore("fr-CA"))]);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ErrorDetails.ShouldHaveValidationError("languageTags", "The culture 'fr' is not one of the supported cultures.");
    }

    [Fact]
    public void A_culture_with_two_language_tags_is_rejected()
    {
        // Act
        var result = CultureLanguageTags.Create(
            Supported,
            [new CultureLanguageTag(Pt, PtBr), new CultureLanguageTag(Pt, ContentCulture.Restore("pt-PT"))]);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ErrorDetails.ShouldHaveValidationError("languageTags", "The culture 'pt' has more than one language tag.");
    }

    [Fact]
    public void A_language_tag_of_two_cultures_is_rejected()
    {
        // Act
        var result = CultureLanguageTags.Create(
            Supported,
            [new CultureLanguageTag(Pt, PtBr), new CultureLanguageTag(En, PtBr)]);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ErrorDetails.ShouldHaveValidationError("languageTags", "The language tag 'pt-BR' belongs to more than one culture.");
    }

    [Fact]
    public void A_language_tag_that_another_culture_declares_for_itself_is_rejected()
    {
        // Act
        var result = CultureLanguageTags.Create(Supported, [new CultureLanguageTag(Pt, En)]);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ErrorDetails.ShouldHaveValidationError("languageTags", "The language tag 'en' belongs to more than one culture.");
    }

    [Fact]
    public void Accessing_a_rejected_set_fails_with_the_reason()
    {
        // Arrange
        var result = CultureLanguageTags.Create(Supported, [new CultureLanguageTag(Fr, En)]);
        var access = () => { _ = result.Value; };

        // Act
        var exception = access.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("The culture 'fr' is not one of the supported cultures.", StringComparison.Ordinal);
    }

    [Fact]
    public void Sets_with_the_same_cultures_and_language_tags_are_equal()
    {
        // Act
        var first = CultureLanguageTags.Create(Supported, [new CultureLanguageTag(Pt, PtBr)]).ShouldBeSuccess();
        var second = CultureLanguageTags.Create(Supported, [new CultureLanguageTag(Pt, PtBr)]).ShouldBeSuccess();
        var identity = CultureLanguageTags.Create(Supported, []).ShouldBeSuccess();

        // Assert
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(identity);
    }

    [Fact]
    public void Is_unaffected_by_later_changes_to_the_source_list()
    {
        // Arrange
        var source = new List<CultureLanguageTag> { new(Pt, PtBr) };
        var tags = CultureLanguageTags.Create(Supported, source).ShouldBeSuccess();

        // Act
        source.Add(new CultureLanguageTag(En, ContentCulture.Restore("en-US")));

        // Assert
        tags.LanguageTagOf(En).ShouldBe(En);
    }

    [Fact]
    public void Null_arguments_are_caller_errors()
    {
        // Act
        var nullCultures = () => { CultureLanguageTags.Create(null!, []); };
        var nullList = () => { CultureLanguageTags.Create(Supported, null!); };
        var nullMember = () => { CultureLanguageTags.Create(Supported, [null!]); };
        var nullCulture = () => { _ = new CultureLanguageTag(null!, PtBr); };
        var nullLanguageTag = () => { _ = new CultureLanguageTag(Pt, null!); };

        // Assert
        nullCultures.ShouldThrow<ArgumentNullException>();
        nullList.ShouldThrow<ArgumentNullException>();
        nullMember.ShouldThrow<ArgumentNullException>();
        nullCulture.ShouldThrow<ArgumentNullException>();
        nullLanguageTag.ShouldThrow<ArgumentNullException>();
    }
}
