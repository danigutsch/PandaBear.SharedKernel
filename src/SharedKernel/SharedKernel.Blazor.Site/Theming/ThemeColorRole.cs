namespace SharedKernel.Blazor.Site.Theming;

/// <summary>A colour token of the site theme; each maps to one <c>--site-color-*</c> custom property.</summary>
public enum ThemeColorRole
{
    /// <summary>Links, primary buttons and current items (<c>--site-color-primary</c>).</summary>
    Primary = 0,

    /// <summary>Primary and outline buttons on hover (<c>--site-color-primary-hover</c>); derived when not given.</summary>
    PrimaryHover = 1,

    /// <summary>Text on the primary colour (<c>--site-color-on-primary</c>).</summary>
    OnPrimary = 2,

    /// <summary>Dividers and secondary buttons (<c>--site-color-accent</c>).</summary>
    Accent = 3,

    /// <summary>Secondary buttons on hover (<c>--site-color-accent-hover</c>); derived when not given.</summary>
    AccentHover = 4,

    /// <summary>Text on the accent colour (<c>--site-color-on-accent</c>).</summary>
    OnAccent = 5,

    /// <summary>Body text and headings (<c>--site-color-text</c>).</summary>
    Text = 6,

    /// <summary>Secondary text (<c>--site-color-muted</c>).</summary>
    Muted = 7,

    /// <summary>The page background (<c>--site-color-surface</c>).</summary>
    Surface = 8,

    /// <summary>Cards, quotes and the contact card (<c>--site-color-surface-alt</c>).</summary>
    SurfaceAlt = 9,

    /// <summary>Borders and rules (<c>--site-color-border</c>).</summary>
    Border = 10,

    /// <summary>The keyboard focus ring (<c>--site-color-focus</c>).</summary>
    Focus = 11,
}
