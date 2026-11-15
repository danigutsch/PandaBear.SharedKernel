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

    /// <summary>Gets Brazilian Portuguese, the language tag of the Portuguese culture in <see cref="LanguageTags" />.</summary>
    public static ContentCulture PtBr { get; } = ContentCulture.Restore("pt-BR");

    /// <summary>Gets the supported set: Portuguese (default) and English.</summary>
    public static SupportedCultures Supported { get; } = SupportedCultures.Create(Pt, [Pt, En]).ShouldBeSuccess();

    /// <summary>Gets the language tags of <see cref="Supported" />: Portuguese declares <c>pt-BR</c>, English itself.</summary>
    public static CultureLanguageTags LanguageTags { get; } =
        CultureLanguageTags.Create(Supported, [new CultureLanguageTag(Pt, PtBr)]).ShouldBeSuccess();
}
