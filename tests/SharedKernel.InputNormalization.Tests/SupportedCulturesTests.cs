using PandaBear.Testing;

namespace PandaBear.InputNormalization.Tests;

/// <summary>Unit tests for <see cref="SupportedCultures" />.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.InputNormalizationCapability)]
public sealed class SupportedCulturesTests
{
    [Fact]
    public void Exposes_the_default_and_the_allowed_cultures_in_declaration_order()
    {
        // Act
        var cultures = new SupportedCultures("PT", ["en", "PT"]);

        // Assert
        cultures.Default.ShouldBe("PT");
        cultures.Cultures.ShouldBe(["en", "PT"]);
    }

    [Theory]
    [InlineData("pt", "pt")]
    [InlineData("PT", "pt")]
    [InlineData("En", "en")]
    public void TryGet_matches_case_insensitively_and_returns_the_declared_spelling(string value, string expected)
    {
        // Act
        var found = new SupportedCultures("pt", ["pt", "en"]).TryGet(value, out var culture);

        // Assert
        found.ShouldBeTrue();
        culture.ShouldBe(expected);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("pt-BR")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void TryGet_rejects_cultures_outside_the_set(string? value)
    {
        // Act
        var found = new SupportedCultures("pt", ["pt", "en"]).TryGet(value, out var culture);

        // Assert
        found.ShouldBeFalse();
        culture.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("pt-BR", "pt")]
    [InlineData("en-US", "en")]
    [InlineData("pt-Latn-BR", "pt")]
    [InlineData("EN", "en")]
    public void Resolve_falls_back_to_the_neutral_parent(string requested, string expected)
    {
        // Act
        var resolved = new SupportedCultures("pt", ["pt", "en"]).Resolve(requested);

        // Assert
        resolved.ShouldBe(expected);
    }

    [Fact]
    public void Resolve_maps_a_neutral_request_to_the_first_supported_regional_culture()
    {
        // Arrange
        var cultures = new SupportedCultures("pt-BR", ["pt-BR", "en-US"]);

        // Act
        var resolved = cultures.Resolve("en");

        // Assert
        resolved.ShouldBe("en-US");
    }

    [Fact]
    public void Resolve_prefers_the_exact_match_over_the_parent()
    {
        // Arrange
        var cultures = new SupportedCultures("pt", ["pt", "pt-PT"]);

        // Act
        var resolved = cultures.Resolve("pt-PT");

        // Assert
        resolved.ShouldBe("pt-PT");
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("*")]
    [InlineData(null)]
    public void Resolve_returns_null_when_nothing_matches(string? requested)
    {
        // Act
        var resolved = new SupportedCultures("pt", ["pt", "en"]).Resolve(requested);

        // Assert
        resolved.ShouldBeNull();
    }

    [Fact]
    public void Rejects_a_default_that_is_not_allowed()
    {
        // Act
        var create = () => new SupportedCultures("fr", ["pt", "en"]);

        // Assert
        create.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("p t")]
    [InlineData("pt/br")]
    [InlineData("pt?")]
    [InlineData("../x")]
    [InlineData("-pt")]
    [InlineData("pt-")]
    [InlineData("pt--BR")]
    public void Rejects_culture_names_that_are_not_path_safe_language_tags(string name)
    {
        // Act
        var create = () => new SupportedCultures("pt", ["pt", name]);

        // Assert
        create.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Rejects_duplicates_ignoring_case()
    {
        // Act
        var create = () => new SupportedCultures("pt", ["pt", "PT"]);

        // Assert
        create.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        // Act
        var nullDefault = () => new SupportedCultures(null!, ["pt"]);
        var nullList = () => new SupportedCultures("pt", null!);

        // Assert
        nullDefault.ShouldThrow<ArgumentNullException>();
        nullList.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Is_unaffected_by_later_changes_to_the_source_list()
    {
        // Arrange
        var source = new List<string> { "pt", "en" };
        var cultures = new SupportedCultures("pt", source);

        // Act
        source.Add("fr");

        // Assert
        cultures.Cultures.Count.ShouldBe(2);
    }
}
