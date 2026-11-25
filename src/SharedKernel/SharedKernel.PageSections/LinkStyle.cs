namespace SharedKernel.PageSections;

/// <summary>
/// The look of a section's button. It mirrors the button variants of <c>PandaBear.SharedKernel.Blazor.Site</c>, which the renderer maps
/// it to, so the model depends on no display package.
/// </summary>
public enum LinkStyle
{
    /// <summary>The filled, primary-colour button.</summary>
    Primary = 0,

    /// <summary>The filled, accent-colour button.</summary>
    Secondary = 1,

    /// <summary>The transparent button with a border.</summary>
    Outline = 2,
}
