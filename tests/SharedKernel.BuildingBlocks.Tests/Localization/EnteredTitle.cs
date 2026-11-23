namespace PandaBear.BuildingBlocks.Tests;

/// <summary>An entered translated title used to exercise <see cref="TranslationListRules" />.</summary>
/// <param name="Culture">The entered culture name.</param>
/// <param name="Text">The entered text.</param>
internal sealed record EnteredTitle(string? Culture, string Text);
