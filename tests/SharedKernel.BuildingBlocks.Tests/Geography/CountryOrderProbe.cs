namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Looks up positions in the ordered country list.</summary>
internal static class CountryOrderProbe
{
    /// <summary>Gets the zero-based position of a code in <see cref="CountryCode.AllByEnglishName"/>.</summary>
    /// <param name="code">The upper-case alpha-2 code.</param>
    /// <returns>The index, or -1 when absent.</returns>
    internal static int IndexOf(string code) =>
        CountryCode.AllByEnglishName.ToList().FindIndex(country => country.Value == code);
}
