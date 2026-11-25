using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>Tests culture fallback over the translations of one entity.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "Localization")]
public sealed class TranslationSetTests
{
    private static readonly ContentCulture Pt = ContentCulture.Restore("pt");
    private static readonly ContentCulture En = ContentCulture.Restore("en");

    [Fact]
    public void The_exact_culture_wins()
    {
        // Arrange
        var set = TranslationSet.Create(
            [TitleTranslation.For("pt"), TitleTranslation.For("pt-BR"), TitleTranslation.For("en")],
            static t => t.Culture).ShouldBeSuccess();

        // Act
        var found = set.TryResolve(ContentCulture.Restore("pt-BR"), En, out var translation);

        // Assert
        found.ShouldBeTrue();
        translation.ShouldNotBeNull().Text.ShouldBe("title-pt-BR");
    }

    [Fact]
    public void The_neutral_parent_of_the_requested_culture_is_next()
    {
        // Arrange
        var set = TranslationSet.Create([TitleTranslation.For("pt"), TitleTranslation.For("en")], static t => t.Culture).ShouldBeSuccess();

        // Act
        var found = set.TryResolve(ContentCulture.Restore("pt-BR"), En, out var translation);

        // Assert
        found.ShouldBeTrue();
        translation.ShouldNotBeNull().Text.ShouldBe("title-pt");
    }

    [Fact]
    public void A_multi_part_culture_walks_up_to_its_language()
    {
        // Arrange
        var set = TranslationSet.Create([TitleTranslation.For("zh"), TitleTranslation.For("en")], static t => t.Culture).ShouldBeSuccess();

        // Act
        var found = set.TryResolve(ContentCulture.Restore("zh-Hant-TW"), En, out var translation);

        // Assert
        found.ShouldBeTrue();
        translation.ShouldNotBeNull().Text.ShouldBe("title-zh");
    }

    [Fact]
    public void A_same_language_sibling_comes_before_the_default_culture()
    {
        // Arrange
        var set = TranslationSet.Create([TitleTranslation.For("en"), TitleTranslation.For("pt-BR")], static t => t.Culture).ShouldBeSuccess();

        // Act
        var neutral = set.TryResolve(Pt, En, out var fromNeutral);
        var regional = set.TryResolve(ContentCulture.Restore("pt-PT"), En, out var fromRegional);

        // Assert
        neutral.ShouldBeTrue();
        fromNeutral.ShouldNotBeNull().Text.ShouldBe("title-pt-BR");
        regional.ShouldBeTrue();
        fromRegional.ShouldNotBeNull().Text.ShouldBe("title-pt-BR");
    }

    [Fact]
    public void The_first_same_language_sibling_in_set_order_wins()
    {
        // Arrange
        var portugalFirst = TranslationSet.Create(
            [TitleTranslation.For("en"), TitleTranslation.For("pt-PT"), TitleTranslation.For("pt-BR")],
            static t => t.Culture).ShouldBeSuccess();
        var brazilFirst = TranslationSet.Create(
            [TitleTranslation.For("pt-BR"), TitleTranslation.For("en"), TitleTranslation.For("pt-PT")],
            static t => t.Culture).ShouldBeSuccess();

        // Act
        portugalFirst.TryResolve(Pt, En, out var fromPortugalFirst);
        brazilFirst.TryResolve(Pt, En, out var fromBrazilFirst);

        // Assert
        fromPortugalFirst.ShouldNotBeNull().Text.ShouldBe("title-pt-PT");
        fromBrazilFirst.ShouldNotBeNull().Text.ShouldBe("title-pt-BR");
    }

    [Fact]
    public void A_neutral_parent_comes_before_a_same_language_sibling()
    {
        // Arrange
        var set = TranslationSet.Create([TitleTranslation.For("pt-PT"), TitleTranslation.For("pt")], static t => t.Culture).ShouldBeSuccess();

        // Act
        set.TryResolve(ContentCulture.Restore("pt-BR"), En, out var translation);

        // Assert
        translation.ShouldNotBeNull().Text.ShouldBe("title-pt");
    }

    [Fact]
    public void The_resolution_order_matches_supported_cultures()
    {
        // Arrange
        ContentCulture[] cultures = [En, ContentCulture.Restore("pt-BR"), ContentCulture.Restore("zh-Hans")];
        var supported = SupportedCultures.Create(En, cultures).ShouldBeSuccess();
        var set = TranslationSet.Create(cultures.Select(static c => TitleTranslation.For(c.Value)), static t => t.Culture).ShouldBeSuccess();
        string[] requests = ["pt", "pt-PT", "zh-Hant-TW", "en-GB", "fr"];

        // Act
        var fromSupported = requests.Select(r => supported.Resolve(r)?.Value ?? supported.Default.Value).ToList();
        var fromSet = requests.Select(r =>
        {
            set.TryResolve(ContentCulture.Restore(r), En, out var translation);
            return translation.ShouldNotBeNull().Culture.Value;
        }).ToList();

        // Assert
        fromSet.ShouldBe(fromSupported);
    }

    [Fact]
    public void The_default_culture_is_used_when_the_requested_one_is_missing()
    {
        // Arrange
        var set = TranslationSet.Create([TitleTranslation.For("en")], static t => t.Culture).ShouldBeSuccess();

        // Act
        var found = set.TryResolve(ContentCulture.Restore("fr-FR"), En, out var translation);

        // Assert
        found.ShouldBeTrue();
        translation.ShouldNotBeNull().Text.ShouldBe("title-en");
    }

    [Fact]
    public void The_neutral_parent_of_the_default_culture_is_the_last_resort()
    {
        // Arrange
        var set = TranslationSet.Create([TitleTranslation.For("en")], static t => t.Culture).ShouldBeSuccess();

        // Act
        var found = set.TryResolve(ContentCulture.Restore("fr"), ContentCulture.Restore("en-US"), out var translation);

        // Assert
        found.ShouldBeTrue();
        translation.ShouldNotBeNull().Text.ShouldBe("title-en");
    }

    [Fact]
    public void A_missing_request_uses_the_default_culture()
    {
        // Arrange
        var set = TranslationSet.Create([TitleTranslation.For("en"), TitleTranslation.For("pt")], static t => t.Culture).ShouldBeSuccess();

        // Act
        var found = set.TryResolve(null, Pt, out var translation);

        // Assert
        found.ShouldBeTrue();
        translation.ShouldNotBeNull().Text.ShouldBe("title-pt");
    }

    [Fact]
    public void No_translation_is_reported_when_nothing_matches()
    {
        // Arrange
        var set = TranslationSet.Create([TitleTranslation.For("de")], static t => t.Culture).ShouldBeSuccess();

        // Act
        var found = set.TryResolve(ContentCulture.Restore("fr"), En, out var translation);

        // Assert
        found.ShouldBeFalse();
        translation.ShouldBeNull();
    }

    [Fact]
    public void An_empty_set_resolves_nothing()
    {
        // Arrange
        var set = TranslationSet.Create<TitleTranslation>([], static t => t.Culture).ShouldBeSuccess();

        // Act
        var found = set.TryResolve(En, En, out _);

        // Assert
        found.ShouldBeFalse();
    }

    [Fact]
    public void Two_translations_for_one_culture_are_rejected()
    {
        // Act
        var result = TranslationSet.Create(
            [TitleTranslation.For("pt"), TitleTranslation.For("PT")],
            static t => t.Culture);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void Null_arguments_are_caller_errors()
    {
        // Arrange
        var set = TranslationSet.Create<TitleTranslation>([], static t => t.Culture).ShouldBeSuccess();

        // Act
        var nullItems = () => { TranslationSet.Create<TitleTranslation>(null!, static t => t.Culture); };
        var nullSelector = () => { TranslationSet.Create<TitleTranslation>([], null!); };
        var nullMember = () => { TranslationSet.Create<TitleTranslation>([null!], static t => t.Culture); };
        var nullDefault = () => { set.TryResolve(En, null!, out _); };

        // Assert
        nullItems.ShouldThrow<ArgumentNullException>();
        nullSelector.ShouldThrow<ArgumentNullException>();
        nullMember.ShouldThrow<ArgumentNullException>();
        nullDefault.ShouldThrow<ArgumentNullException>();
    }
}
