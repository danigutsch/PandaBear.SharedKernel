using PandaBear.BuildingBlocks;

namespace PandaBear.AspNetCore.Localization.Tests;

/// <summary>Shared test cultures: Portuguese (default) and English.</summary>
internal static class LocalizationTestCultures
{
    /// <summary>Gets the Portuguese culture.</summary>
    public static ContentCulture Pt { get; } = ContentCulture.Restore("pt");

    /// <summary>Gets the English culture.</summary>
    public static ContentCulture En { get; } = ContentCulture.Restore("en");

    /// <summary>Gets the French culture, which is not supported.</summary>
    public static ContentCulture Fr { get; } = ContentCulture.Restore("fr");

    /// <summary>Gets the supported set: Portuguese (default) and English.</summary>
    public static SupportedCultures Supported { get; } = SupportedCultures.Create(Pt, [Pt, En]).ShouldBeSuccess();
}
