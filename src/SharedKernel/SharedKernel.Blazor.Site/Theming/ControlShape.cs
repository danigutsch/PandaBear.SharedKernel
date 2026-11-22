namespace PandaBear.Blazor.Site.Theming;

/// <summary>
/// The corner shape of a theme. It sets <c>--site-control-radius</c>, the radius of buttons, pager links and language links, and
/// <c>--site-radius</c>, the radius of surfaces such as cards, disclosures and images, so every component follows one choice.
/// </summary>
public enum ControlShape
{
    /// <summary>Controls and surfaces with 0.5rem corners, the look of <c>site.css</c> without a theme.</summary>
    Rounded = 0,

    /// <summary>Controls and surfaces with square corners.</summary>
    Square = 1,

    /// <summary>
    /// Controls with fully round ends (pills) and surfaces with 0.5rem corners, because a card or a disclosure has no ends to round.
    /// </summary>
    Pill = 2,
}
