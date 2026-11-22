namespace PandaBear.Blazor.Site;

/// <summary>
/// The side of a <see cref="Split" /> on which its media column lies from the <c>48rem</c> breakpoint on. The side is visual only: in
/// the markup, and so in reading and focus order, the text always comes before the media, and below the breakpoint the media follows
/// the text.
/// </summary>
public enum SplitMediaSide
{
    /// <summary>The media on the inline-end side, after the text: on the right in a left-to-right language.</summary>
    End = 0,

    /// <summary>The media on the inline-start side, before the text: on the left in a left-to-right language.</summary>
    Start = 1,
}
