namespace PandaBear.Blazor.Site;

/// <summary>How tall a <see cref="HeroLayout.Cover" /> hero is at least; its content can make it taller.</summary>
public enum CoverHeight
{
    /// <summary>The cover of an ordinary page: 20rem below the <c>48rem</c> breakpoint and 34.375rem (550px) from it on.</summary>
    Page = 0,

    /// <summary>
    /// The cover of a landing page such as the home page: 20rem below the breakpoint and from it on 85% of the small viewport height,
    /// at least 34.375rem (550px) and at most 52rem (832px).
    /// </summary>
    Landing = 1,
}
