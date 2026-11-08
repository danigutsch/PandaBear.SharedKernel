using System.Globalization;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the culture-independent English-name ordering of country codes.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ContactValueObjects")]
public sealed class CountryCodeOrderTests
{
    [Fact]
    public void All_by_english_name_holds_every_code_once()
    {
        // Act
        var ordered = CountryCode.AllByEnglishName;

        // Assert
        ordered.Count.ShouldBe(CountryCode.AssignedCount);
        ordered.Select(country => country.Value).Distinct().Count().ShouldBe(CountryCode.AssignedCount);
        ordered.ShouldBeSameAs(CountryCode.AllByEnglishName);
    }

    [Fact]
    public void All_by_english_name_equals_the_table_sorted_by_the_independent_key()
    {
        // Act
        var expected = CountryCode.All
            .OrderBy(country => CountryNameAlphabet.ExpectedKey(country.EnglishName), StringComparer.Ordinal)
            .ThenBy(country => country.Value, StringComparer.Ordinal)
            .Select(country => country.Value);

        // Assert
        CountryCode.AllByEnglishName.Select(country => country.Value).ShouldBe(expected);
    }

    [Fact]
    public void Every_english_name_uses_only_characters_the_sort_key_handles()
    {
        // Act
        var offenders = CountryCode.All
            .Where(country => !country.EnglishName.All(CountryNameAlphabet.IsSupported))
            .Select(country => country.EnglishName)
            .ToList();

        // Assert
        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void Accented_names_sort_among_their_unaccented_neighbours()
    {
        // Act
        var aland = CountryOrderProbe.IndexOf("AX");
        var cote = CountryOrderProbe.IndexOf("CI");
        var costaRica = CountryOrderProbe.IndexOf("CR");
        var cuba = CountryOrderProbe.IndexOf("CU");
        var curacao = CountryOrderProbe.IndexOf("CW");
        var cyprus = CountryOrderProbe.IndexOf("CY");

        // Assert
        aland.ShouldBeGreaterThan(CountryOrderProbe.IndexOf("AF"));
        aland.ShouldBeLessThan(CountryOrderProbe.IndexOf("AL"));
        costaRica.ShouldBeLessThan(cote);
        curacao.ShouldBeGreaterThan(cuba);
        curacao.ShouldBeLessThan(cyprus);
        CountryOrderProbe.IndexOf("TR").ShouldBeGreaterThan(CountryOrderProbe.IndexOf("TN"));
        CountryOrderProbe.IndexOf("TR").ShouldBeLessThan(CountryOrderProbe.IndexOf("TM"));
        CountryOrderProbe.IndexOf("RE").ShouldBeLessThan(CountryOrderProbe.IndexOf("RO"));
        CountryOrderProbe.IndexOf("BL").ShouldBeLessThan(CountryOrderProbe.IndexOf("SH"));
    }

    [Fact]
    public void Zimbabwe_is_last_and_the_first_name_is_afghanistan()
    {
        // Assert
        CountryCode.AllByEnglishName[^1].Value.ShouldBe("ZW");
        CountryCode.AllByEnglishName[0].Value.ShouldBe("AF");
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("tr-TR")]
    [InlineData("")]
    public void Order_does_not_depend_on_the_current_culture(string cultureName)
    {
        // Arrange
        var baseline = CountryCode.AllByEnglishName.Select(country => country.Value).ToArray();

        // Act
        var underCulture = CultureOverride.Run(
            CultureInfo.GetCultureInfo(cultureName),
            () => CountryCode.All.OrderBy(country => CountryNameAlphabet.ExpectedKey(country.EnglishName), StringComparer.Ordinal)
                .ThenBy(country => country.Value, StringComparer.Ordinal)
                .Select(country => country.Value)
                .ToArray());

        // Assert
        underCulture.ShouldBe(baseline);
    }
}
