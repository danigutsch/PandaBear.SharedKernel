using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>Translation-list rules over <c>pt</c> (the default) and <c>en</c>, on the field <c>translations</c>, for albums.</summary>
internal static class TitleListRules
{
    /// <summary>Gets the rules.</summary>
    public static TranslationListRules Rules { get; } =
        new(SupportedCultures.Create(ContentCulture.Restore("pt"), [ContentCulture.Restore("pt"), ContentCulture.Restore("en")]).ShouldBeSuccess(), "translations", "album");

    /// <summary>Validates entered titles into translations, naming <c>definition</c> for a <see langword="null"/> one.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="entered">The entered titles.</param>
    /// <returns>The translations.</returns>
    public static IReadOnlyList<TitleTranslation> Validate(ValidationErrors errors, IReadOnlyList<EnteredTitle?>? entered) =>
        Rules.Validate(
            errors,
            entered,
            static title => title.Culture,
            static (culture, title) => new TitleTranslation(culture, title.Text),
            "definition");
}
