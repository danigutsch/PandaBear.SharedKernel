namespace PandaBear.Blazor.Site;

/// <summary>
/// The tone of a <see cref="StatusBadge" />: a background and text colour pair from the stylesheet's status tokens. The tone only
/// supports the badge's text, which always says the status, so the colour is never the only cue.
/// </summary>
public enum StatusTone
{
    /// <summary>A state without emphasis, such as "Scheduled" (<c>site-status--neutral</c>).</summary>
    Neutral = 0,

    /// <summary>Information, such as "New" (<c>site-status--info</c>).</summary>
    Info = 1,

    /// <summary>A good state, such as "Open" (<c>site-status--success</c>).</summary>
    Success = 2,

    /// <summary>A state that needs attention, such as "Few places left" (<c>site-status--warning</c>).</summary>
    Warning = 3,

    /// <summary>A blocking state, such as "Sold out" (<c>site-status--danger</c>).</summary>
    Danger = 4,
}
