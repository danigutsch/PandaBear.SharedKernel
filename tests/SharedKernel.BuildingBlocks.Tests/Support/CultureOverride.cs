using System.Globalization;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Runs code under a temporary current culture and UI culture, restoring both afterwards.</summary>
internal static class CultureOverride
{
    /// <summary>Evaluates <paramref name="action"/> with the current culture and UI culture set to <paramref name="culture"/>.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="culture">The culture to apply while the action runs.</param>
    /// <param name="action">The code to run.</param>
    /// <returns>The action's result.</returns>
    public static T Run<T>(CultureInfo culture, Func<T> action)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
