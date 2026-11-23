namespace PandaBear.Blazor.Site;

/// <summary>The size of a <see cref="Hero" />'s title relative to the theme's level 1 heading.</summary>
public enum HeroTitleScale
{
    /// <summary>The level 1 heading's size.</summary>
    Regular = 0,

    /// <summary>
    /// The level 1 heading's size times <c>--site-hero-title-scale-large</c>, 1.25 by default (<c>site-hero--title-large</c>), for a
    /// landing page whose title is its largest text.
    /// </summary>
    Large = 1,
}
