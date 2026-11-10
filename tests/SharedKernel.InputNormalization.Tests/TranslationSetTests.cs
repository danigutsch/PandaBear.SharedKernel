using PandaBear.Testing;

namespace PandaBear.InputNormalization.Tests;

/// <summary>Unit tests for <see cref="TranslationSet{T}" />.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.InputNormalizationCapability)]
public sealed class TranslationSetTests
{
    [Fact]
    public void Resolves_the_exact_culture_first()
    {
        // Arrange
        var set = TitleTranslation.SetOf("pt", "pt-BR", "en");

        // Act
        var found = set.TryResolve("pt-BR", "en", out var translation);

        // Assert
        found.ShouldBeTrue();
        translation.ShouldNotBeNull().Text.ShouldBe("title-pt-BR");
    }

    [Fact]
    public void Matches_cultures_ignoring_case()
    {
        // Arrange
        var set = TitleTranslation.SetOf("pt-BR");

        // Act
        var found = set.TryResolve("PT-br", "en", out var translation);

        // Assert
        found.ShouldBeTrue();
        translation.ShouldNotBeNull().Text.ShouldBe("title-pt-BR");
    }

    [Fact]
    public void Falls_back_to_the_neutral_parent_of_the_requested_culture()
    {
        // Arrange
        var set = TitleTranslation.SetOf("pt", "en");

        // Act
        var found = set.TryResolve("pt-BR", "en", out var translation);

        // Assert
        found.ShouldBeTrue();
        translation.ShouldNotBeNull().Text.ShouldBe("title-pt");
    }

    [Fact]
    public void Walks_a_multi_part_culture_up_to_its_language()
    {
        // Arrange
        var set = TitleTranslation.SetOf("zh", "en");

        // Act
        var found = set.TryResolve("zh-Hant-TW", "en", out var translation);

        // Assert
        found.ShouldBeTrue();
        translation.ShouldNotBeNull().Text.ShouldBe("title-zh");
    }

    [Fact]
    public void Falls_back_to_the_default_culture_when_the_requested_one_is_missing()
    {
        // Arrange
        var set = TitleTranslation.SetOf("en");

        // Act
        var found = set.TryResolve("fr-FR", "en", out var translation);

        // Assert
        found.ShouldBeTrue();
        translation.ShouldNotBeNull().Text.ShouldBe("title-en");
    }

    [Fact]
    public void Falls_back_to_the_neutral_parent_of_the_default_culture()
    {
        // Arrange
        var set = TitleTranslation.SetOf("en");

        // Act
        var found = set.TryResolve("fr", "en-US", out var translation);

        // Assert
        found.ShouldBeTrue();
        translation.ShouldNotBeNull().Text.ShouldBe("title-en");
    }

    [Theory]
    [InlineData("fr")]
    [InlineData(null)]
    [InlineData("")]
    public void Uses_the_default_when_the_request_is_missing_or_blank(string? requested)
    {
        // Arrange
        var set = TitleTranslation.SetOf("en", "pt");

        // Act
        var found = set.TryResolve(requested, "pt", out var translation);

        // Assert
        found.ShouldBeTrue();
        translation.ShouldNotBeNull().Text.ShouldBe("title-pt");
    }

    [Fact]
    public void Reports_no_translation_when_nothing_matches()
    {
        // Arrange
        var set = TitleTranslation.SetOf("de");

        // Act
        var found = set.TryResolve("fr", "en", out var translation);

        // Assert
        found.ShouldBeFalse();
        translation.ShouldBeNull();
    }

    [Fact]
    public void An_empty_set_resolves_nothing()
    {
        // Act
        var found = TitleTranslation.SetOf().TryResolve("en", "en", out _);

        // Assert
        found.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_two_translations_for_the_same_culture_ignoring_case()
    {
        // Act
        var create = () => TitleTranslation.SetOf("pt", "PT");

        // Assert
        create.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Rejects_a_blank_culture_selected_from_a_translation()
    {
        // Act
        var create = () => TitleTranslation.SetOf(" ");

        // Assert
        create.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        // Act
        var nullItems = () => new TranslationSet<TitleTranslation>(null!, static t => t.Culture);
        var nullSelector = () => new TranslationSet<TitleTranslation>([], null!);

        // Assert
        nullItems.ShouldThrow<ArgumentNullException>();
        nullSelector.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Rejects_a_null_default_culture()
    {
        // Arrange
        var set = TitleTranslation.SetOf("en");

        // Act
        var resolve = () => { set.TryResolve("en", null!, out _); };

        // Assert
        resolve.ShouldThrow<ArgumentNullException>();
    }
}
