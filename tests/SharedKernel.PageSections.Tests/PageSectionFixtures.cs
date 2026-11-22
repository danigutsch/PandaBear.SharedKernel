using PandaBear.Results;

namespace PandaBear.PageSections.Tests;

/// <summary>Cultures, rules and helpers shared by the page section tests.</summary>
internal static class PageSectionFixtures
{
    /// <summary>The field prefix of the Portuguese sections.</summary>
    public const string Prefix = "pt.sections";

    /// <summary>Gets Portuguese.</summary>
    public static ContentCulture Portuguese { get; } = ContentCulture.Restore("pt");

    /// <summary>Gets English.</summary>
    public static ContentCulture English { get; } = ContentCulture.Restore("en");

    /// <summary>Gets the default rules, V2's limits.</summary>
    public static PageSectionRules Rules { get; } = new();

    /// <summary>Builds a section with a heading and text.</summary>
    /// <param name="heading">The heading.</param>
    /// <param name="text">The text.</param>
    /// <returns>The definition.</returns>
    public static SiteSectionDefinition Text(string? heading = "Quem somos", string? text = "Somos uma agência.") => new() { Heading = heading, Text = text };

    /// <summary>Validates sections as a Portuguese draft or published translation.</summary>
    /// <param name="context">The context.</param>
    /// <param name="published">Whether the translation is published.</param>
    /// <param name="definitions">The sections.</param>
    /// <returns>The result.</returns>
    public static Result<IReadOnlyList<SiteSection>> Create(SiteContext context, bool published, params SiteSectionDefinition[] definitions) =>
        PageSectionList.Create(definitions, Rules, context, Portuguese, published, Prefix);

    /// <summary>Validates one draft section that must be valid.</summary>
    /// <param name="context">The context.</param>
    /// <param name="definition">The section.</param>
    /// <returns>The section.</returns>
    public static SiteSection Valid(SiteContext context, SiteSectionDefinition definition) =>
        Create(context, false, definition).ShouldBeSuccess().ShouldHaveSingleItem();

    /// <summary>Validates one draft section that must be refused, and returns its error.</summary>
    /// <param name="context">The context.</param>
    /// <param name="definition">The section.</param>
    /// <returns>The error.</returns>
    public static ResultError? Refused(SiteContext context, SiteSectionDefinition definition)
    {
        var result = Create(context, false, definition);
        result.IsFailure.ShouldBeTrue();
        return result.ErrorDetails;
    }

    /// <summary>Reads the field paths of an error, in order.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The field paths.</returns>
    public static string[] Fields(ResultError? error) =>
        [.. (error?.ValidationErrors ?? throw new InvalidOperationException("No validation errors.")).Keys.Order(StringComparer.Ordinal)];

    /// <summary>Asserts that restoring a definition is refused as structurally broken.</summary>
    /// <param name="definition">The stored definition.</param>
    public static void ShouldNotRestore(SiteSectionDefinition definition)
    {
        Action act = () => PageSection.Restore(definition);
        act.ShouldThrow<ArgumentException>();
    }
}
