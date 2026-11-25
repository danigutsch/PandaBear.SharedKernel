namespace SharedKernel.PageSections;

/// <summary>An unvalidated button of a section.</summary>
/// <typeparam name="TLinkInput">The application's raw link target, as an editor enters it; its context turns it into a validated target.</typeparam>
/// <param name="Text">The button text; required.</param>
/// <param name="Target">Where the button links; required, and the application's context validates it.</param>
/// <param name="Style">The look of the button.</param>
public sealed record PageLinkDefinition<TLinkInput>(string? Text, TLinkInput? Target, LinkStyle Style = LinkStyle.Primary)
    where TLinkInput : class;
