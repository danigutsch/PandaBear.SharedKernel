namespace SharedKernel.Blazor.Site;

/// <summary>The size of a <see cref="ButtonLink" /> and of the more look of a <see cref="Card" />, relative to the button's own font size.</summary>
public enum ButtonSize
{
    /// <summary>2.75 times the font size tall, with an inline padding of half that height.</summary>
    Regular = 0,

    /// <summary>
    /// 2.25 times the font size tall, with an inline padding of half that height (<c>site-button--compact</c>); like every button it
    /// stays at least 2.75rem (44px) tall, above the 24px target size of WCAG 2.2 SC 2.5.8.
    /// </summary>
    Compact = 1,
}
