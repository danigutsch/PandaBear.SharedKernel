using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the supported cultures value object.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Localization")]
public sealed class SupportedCulturesTests
{
    private static readonly ContentCulture Pt = ContentCulture.Restore("pt");
    private static readonly ContentCulture En = ContentCulture.Restore("en");
    private static readonly ContentCulture PtBr = ContentCulture.Restore("pt-BR");
    private static readonly ContentCulture EnUs = ContentCulture.Restore("en-US");

    [Fact]
    public void Exposes_the_default_and_the_allowed_cultures_in_declaration_order()
    {
        // Act
        var cultures = SupportedCultures.Create(Pt, [En, Pt]).ShouldBeSuccess();

        // Assert
        cultures.Default.ShouldBe(Pt);
        cultures.Cultures.ShouldBe([En, Pt]);
        cultures.Contains(En).ShouldBeTrue();
        cultures.Contains(PtBr).ShouldBeFalse();
    }

    [Theory]
    [InlineData("pt", "pt")]
    [InlineData("PT", "pt")]
    [InlineData("En", "en")]
    public void TryGet_matches_an_allowed_culture_ignoring_case(string value, string expected)
    {
        // Arrange
        var cultures = SupportedCultures.Create(Pt, [Pt, En]).ShouldBeSuccess();

        // Act
        var found = cultures.TryGet(value, out var culture);

        // Assert
        found.ShouldBeTrue();
        culture.ShouldNotBeNull().Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("pt-BR")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("../x")]
    [InlineData(null)]
    public void TryGet_rejects_text_that_is_not_an_allowed_culture(string? value)
    {
        // Arrange
        var cultures = SupportedCultures.Create(Pt, [Pt, En]).ShouldBeSuccess();

        // Act
        var found = cultures.TryGet(value, out var culture);

        // Assert
        found.ShouldBeFalse();
        culture.ShouldBeNull();
    }

    [Theory]
    [InlineData("pt-BR", "pt")]
    [InlineData("en-US", "en")]
    [InlineData("pt-Latn-BR", "pt")]
    [InlineData("EN", "en")]
    public void Resolve_falls_back_to_the_neutral_parent(string requested, string expected)
    {
        // Arrange
        var cultures = SupportedCultures.Create(Pt, [Pt, En]).ShouldBeSuccess();

        // Act
        var resolved = cultures.Resolve(requested);

        // Assert
        resolved.ShouldNotBeNull().Value.ShouldBe(expected);
    }

    [Fact]
    public void Resolve_maps_a_neutral_request_to_the_first_allowed_regional_culture()
    {
        // Arrange
        var cultures = SupportedCultures.Create(PtBr, [PtBr, EnUs]).ShouldBeSuccess();

        // Act
        var resolved = cultures.Resolve(En);

        // Assert
        resolved.ShouldBe(EnUs);
    }

    [Fact]
    public void Resolve_prefers_the_exact_match_over_the_parent()
    {
        // Arrange
        var portugal = ContentCulture.Restore("pt-PT");
        var cultures = SupportedCultures.Create(Pt, [Pt, portugal]).ShouldBeSuccess();

        // Act
        var resolved = cultures.Resolve(portugal);

        // Assert
        resolved.ShouldBe(portugal);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("*")]
    [InlineData("")]
    [InlineData(null)]
    public void Resolve_returns_null_when_nothing_matches(string? requested)
    {
        // Arrange
        var cultures = SupportedCultures.Create(Pt, [Pt, En]).ShouldBeSuccess();

        // Act
        var resolved = cultures.Resolve(requested);

        // Assert
        resolved.ShouldBeNull();
    }

    [Fact]
    public void A_default_that_is_not_allowed_is_rejected()
    {
        // Act
        var result = SupportedCultures.Create(ContentCulture.Restore("fr"), [Pt, En]);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void An_empty_list_is_rejected()
    {
        // Act
        var result = SupportedCultures.Create(Pt, []);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void A_duplicate_culture_is_rejected()
    {
        // Act
        var result = SupportedCultures.Create(Pt, [Pt, ContentCulture.Restore("PT")]);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void Sets_with_the_same_default_and_cultures_are_equal()
    {
        // Act
        var first = SupportedCultures.Create(Pt, [Pt, En]).ShouldBeSuccess();
        var second = SupportedCultures.Create(Pt, [Pt, En]).ShouldBeSuccess();
        var other = SupportedCultures.Create(En, [Pt, En]).ShouldBeSuccess();

        // Assert
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(other);
    }

    [Fact]
    public void Is_unaffected_by_later_changes_to_the_source_list()
    {
        // Arrange
        var source = new List<ContentCulture> { Pt, En };
        var cultures = SupportedCultures.Create(Pt, source).ShouldBeSuccess();

        // Act
        source.Add(PtBr);

        // Assert
        cultures.Cultures.Count.ShouldBe(2);
    }

    [Fact]
    public void Null_arguments_are_caller_errors()
    {
        // Act
        var nullDefault = () => { SupportedCultures.Create(null!, [Pt]); };
        var nullList = () => { SupportedCultures.Create(Pt, null!); };
        var nullMember = () => { SupportedCultures.Create(Pt, [Pt, null!]); };

        // Assert
        nullDefault.ShouldThrow<ArgumentNullException>();
        nullList.ShouldThrow<ArgumentNullException>();
        nullMember.ShouldThrow<ArgumentNullException>();
    }
}
