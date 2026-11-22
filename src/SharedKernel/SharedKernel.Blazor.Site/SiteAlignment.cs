namespace PandaBear.Blazor.Site;

/// <summary>
/// Where content lies along one axis of its box: the block axis for <see cref="Split.Alignment" /> (top or middle) and the inline axis
/// for <see cref="Section.TextAlignment" /> and <see cref="Band.TextAlignment" /> (the reading start or the middle).
/// </summary>
public enum SiteAlignment
{
    /// <summary>At the start: the top of the block axis, or the side where reading starts on the inline axis.</summary>
    Start = 0,

    /// <summary>In the middle.</summary>
    Center = 1,
}
