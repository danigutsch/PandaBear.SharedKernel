namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>A stored translated title whose text changes, used to exercise <see cref="TranslationListRules.Merge{T}" />.</summary>
/// <param name="culture">The culture of the translation.</param>
/// <param name="text">The translated text.</param>
internal sealed class EditableTitle(ContentCulture culture, string text)
{
    /// <summary>Gets the culture.</summary>
    public ContentCulture Culture { get; } = culture;

    /// <summary>Gets or sets the text.</summary>
    public string Text { get; set; } = text;
}
