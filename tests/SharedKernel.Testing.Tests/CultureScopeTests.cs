using System.Globalization;

namespace SharedKernel.Testing.Tests;

/// <summary>Tests the scope that switches the current culture and UI culture.</summary>
public sealed class CultureScopeTests
{
    [Fact]
    public void A_scope_sets_both_cultures_and_restores_the_previous_ones()
    {
        // Arrange
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var german = CultureInfo.GetCultureInfo("de-DE");
        var french = CultureInfo.GetCultureInfo("fr-FR");

        // Act
        string formatted;
        string uiCulture;
        using (new CultureScope(german, french))
        {
            formatted = 1234.5m.ToString("N1", CultureInfo.CurrentCulture);
            uiCulture = CultureInfo.CurrentUICulture.Name;
        }

        // Assert
        formatted.ShouldBe("1.234,5");
        uiCulture.ShouldBe("fr-FR");
        CultureInfo.CurrentCulture.ShouldBeSameAs(previousCulture);
        CultureInfo.CurrentUICulture.ShouldBeSameAs(previousUiCulture);
    }

    [Fact]
    public void Run_returns_the_result_under_the_culture_and_restores_it_when_the_action_throws()
    {
        // Arrange
        var previousCulture = CultureInfo.CurrentCulture;
        var brazilian = CultureInfo.GetCultureInfo("pt-BR");
        Func<object?> failing = () => CultureScope.Run<string>(brazilian, static () => throw new InvalidOperationException("boom"));

        // Act
        var name = CultureScope.Run(brazilian, static () => CultureInfo.CurrentUICulture.Name);
        failing.ShouldThrow<InvalidOperationException>();

        // Assert
        name.ShouldBe("pt-BR");
        CultureInfo.CurrentCulture.ShouldBeSameAs(previousCulture);
    }
}
