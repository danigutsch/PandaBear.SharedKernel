using System.Globalization;
using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests creation and validation of ISO 3166-1 country codes.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ContactValueObjects")]
public sealed class CountryCodeTests
{
    [Theory]
    [InlineData("BR", "BR")]
    [InlineData(" pt ", "PT")]
    [InlineData("us", "US")]
    public void Country_code_is_trimmed_and_upper_cased(string input, string expected)
    {
        // Act
        var result = CountryCode.Create(input);

        // Assert
        result.ShouldBeSuccess().Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("B")]
    [InlineData("BRA")]
    [InlineData("XX")]
    [InlineData("XK")]
    [InlineData("1A")]
    public void Country_code_rejects_unassigned_input(string? input)
    {
        // Act
        var result = CountryCode.Create(input);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void All_lists_every_assigned_code_once_and_each_one_is_creatable()
    {
        // Act
        var all = CountryCode.All;

        // Assert
        all.Count.ShouldBe(CountryCode.AssignedCount);
        all.Select(code => code.Value).Distinct().Count().ShouldBe(CountryCode.AssignedCount);
        all.All(code => CountryCode.Create(code.Value).IsSuccess).ShouldBeTrue();
        all[0].ToString().ShouldBe("AD");
    }

    [Theory]
    [InlineData("BR", "Brazil")]
    [InlineData("PT", "Portugal")]
    [InlineData("US", "United States")]
    [InlineData("GB", "United Kingdom")]
    [InlineData("AX", "Åland Islands")]
    [InlineData("CI", "Côte d'Ivoire")]
    [InlineData("CW", "Curaçao")]
    [InlineData("RE", "Réunion")]
    [InlineData("BL", "Saint Barthélemy")]
    [InlineData("ST", "São Tomé and Príncipe")]
    [InlineData("TR", "Türkiye")]
    [InlineData("CZ", "Czechia")]
    [InlineData("AG", "Antigua and Barbuda")]
    public void English_name_is_the_fixed_table_name_of_the_country(string code, string expected)
    {
        // Arrange
        var country = CountryCode.Create(code).Value;

        // Act
        var name = country.EnglishName;

        // Assert
        name.ShouldBe(expected);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("tr-TR")]
    [InlineData("")]
    public void English_name_does_not_depend_on_the_current_culture(string cultureName)
    {
        // Arrange
        var expected = CountryCode.All.Select(country => country.EnglishName).ToArray();

        // Act
        var names = CultureOverride.Run(
            CultureInfo.GetCultureInfo(cultureName),
            () => CountryCode.All.Select(country => country.EnglishName).ToArray());

        // Assert
        names.ShouldBe(expected);
        names[CountryCode.All.ToList().FindIndex(country => country.Value == "BR")].ShouldBe("Brazil");
        names[CountryCode.All.ToList().FindIndex(country => country.Value == "DE")].ShouldBe("Germany");
    }

    [Fact]
    public void Every_assigned_country_has_a_readable_name()
    {
        // Act
        var unnamed = CountryCode.All.Where(country => string.IsNullOrWhiteSpace(country.EnglishName)).ToList();

        // Assert
        unnamed.Count.ShouldBe(0);
    }

    [Fact]
    public void Every_assigned_country_has_its_own_name_that_is_not_the_code()
    {
        // Act
        var names = CountryCode.All.Select(country => country.EnglishName).ToList();

        // Assert
        names.Distinct(StringComparer.Ordinal).Count().ShouldBe(CountryCode.AssignedCount);
        CountryCode.All.Count(country => country.EnglishName == country.Value).ShouldBe(0);
    }

    [Fact]
    public void Country_names_have_no_surrounding_whitespace()
    {
        // Act
        var untrimmed = CountryCode.All.Where(country => country.EnglishName != country.EnglishName.Trim()).ToList();

        // Assert
        untrimmed.Count.ShouldBe(0);
    }
}
