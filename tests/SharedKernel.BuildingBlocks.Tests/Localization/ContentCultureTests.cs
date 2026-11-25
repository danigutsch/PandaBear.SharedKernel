using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>Tests the content culture value object.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "Localization")]
public sealed class ContentCultureTests
{
    [Theory]
    [InlineData("pt", "pt")]
    [InlineData("PT", "pt")]
    [InlineData("pt-br", "pt-BR")]
    [InlineData(" PT-Br ", "pt-BR")]
    [InlineData("zh-hant-tw", "zh-Hant-TW")]
    [InlineData("es-419", "es-419")]
    [InlineData("en-US", "en-US")]
    [InlineData("en-u-ca-gregory", "en-u-ca-gregory")]
    [InlineData("EN-U-CA-GREGORY", "en-u-ca-gregory")]
    [InlineData("sr-latn-rs-u-nu-latn", "sr-Latn-RS-u-nu-latn")]
    [InlineData("en-x-ab", "en-x-ab")]
    [InlineData("de-de-X-AB-CDEF", "de-DE-x-ab-cdef")]
    [InlineData("en-a-bb-123", "en-a-bb-123")]
    public void A_culture_is_trimmed_and_written_in_canonical_casing(string input, string expected)
    {
        // Act
        var culture = ContentCulture.Create(input).ShouldBeSuccess();

        // Assert
        culture.Value.ShouldBe(expected);
        culture.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("p t")]
    [InlineData("pt/br")]
    [InlineData("pt?")]
    [InlineData("../x")]
    [InlineData("-pt")]
    [InlineData("pt-")]
    [InlineData("pt--BR")]
    [InlineData("pt_BR")]
    [InlineData("português")]
    [InlineData("en-x")]
    [InlineData("en-u")]
    [InlineData("en-u-x-ab")]
    [InlineData("en-abcdefghi")]
    [InlineData("en-x-abcdefghi")]
    public void An_invalid_culture_is_rejected_without_echoing_it(string? input)
    {
        // Act
        var result = ContentCulture.Create(input);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["culture"].ShouldHaveSingleItem();
        var echoed = !string.IsNullOrWhiteSpace(input) && message.Contains(input, StringComparison.Ordinal);
        echoed.ShouldBeFalse();
    }

    [Fact]
    public void A_well_formed_but_too_long_culture_is_rejected_with_a_fixed_message()
    {
        // Arrange
        // Every part is well formed, so only the length can reject it; the marker parts would show up if the message echoed it.
        const string input = "zz-markerqx-plughzz-xyzzyqq-frobnitz-gruezzz";

        // Act
        var result = ContentCulture.Create(input);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var details = result.ErrorDetails.ShouldNotBeNull();
        var message = details.ValidationErrors.ShouldNotBeNull()["culture"].ShouldHaveSingleItem();
        message.ShouldBe("Culture must be a language tag such as 'en' or 'es-419'.");
        details.Detail.ShouldBe(message);
        message.Contains("markerqx", StringComparison.Ordinal).ShouldBeFalse();
        message.Contains(input, StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void Cultures_that_differ_only_in_case_are_equal()
    {
        // Act
        var first = ContentCulture.Create("pt-br").ShouldBeSuccess();
        var second = ContentCulture.Create("PT-BR").ShouldBeSuccess();

        // Assert
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void A_regional_culture_has_its_language_as_parent()
    {
        // Act
        var culture = ContentCulture.Create("pt-BR").ShouldBeSuccess();

        // Assert
        culture.Parent.ShouldBe(ContentCulture.Restore("pt"));
        culture.Language.ShouldBe(ContentCulture.Restore("pt"));
    }

    [Fact]
    public void A_multi_part_culture_walks_up_one_part_at_a_time()
    {
        // Act
        var culture = ContentCulture.Create("zh-Hant-TW").ShouldBeSuccess();

        // Assert
        var parent = culture.Parent.ShouldNotBeNull();
        parent.ShouldBe(ContentCulture.Restore("zh-Hant"));
        parent.Parent.ShouldBe(ContentCulture.Restore("zh"));
        culture.Language.ShouldBe(ContentCulture.Restore("zh"));
    }

    [Theory]
    [InlineData("en-x-ab", "en")]
    [InlineData("en-u-ca-gregory", "en-u-ca")]
    [InlineData("en-u-ca", "en")]
    [InlineData("en-x-a-b", "en-x-a")]
    public void A_parent_never_ends_in_a_singleton(string input, string expected)
    {
        // Act
        var culture = ContentCulture.Create(input).ShouldBeSuccess();

        // Assert
        culture.Parent.ShouldNotBeNull().Value.ShouldBe(expected);
    }

    [Fact]
    public void A_neutral_culture_has_no_parent_and_is_its_own_language()
    {
        // Act
        var culture = ContentCulture.Create("pt").ShouldBeSuccess();

        // Assert
        culture.Parent.ShouldBeNull();
        culture.Language.ShouldBe(culture);
    }

    [Fact]
    public void Restore_normalizes_a_stored_culture()
    {
        // Act
        var culture = ContentCulture.Restore(" pt-br ");

        // Assert
        culture.Value.ShouldBe("pt-BR");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Restore_rejects_a_blank_value(string? value)
    {
        // Act
        var restore = () => { ContentCulture.Restore(value!); };

        // Assert
        restore.ShouldThrow<ArgumentException>();
    }
}
