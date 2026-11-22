namespace PandaBear.PageSections;

/// <summary>An unvalidated button of a section.</summary>
/// <typeparam name="TLink">The application's link target, such as a menu target.</typeparam>
/// <param name="Text">The button text; required.</param>
/// <param name="Target">Where the button links; the application's context validates it.</param>
/// <param name="Style">The look of the button.</param>
public sealed record PageLinkDefinition<TLink>(string? Text, TLink Target, LinkStyle Style = LinkStyle.Primary)
    where TLink : notnull;
