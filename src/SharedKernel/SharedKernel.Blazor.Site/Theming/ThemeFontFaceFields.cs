namespace PandaBear.Blazor.Site.Theming;

/// <summary>The editor fields of one font file.</summary>
public sealed class ThemeFontFaceFields
{
    /// <summary>Gets or sets the weight.</summary>
    public int? Weight
    {
        get; set;
    }

    /// <summary>Gets or sets the style.</summary>
    public ThemeFontStyle Style
    {
        get; set;
    }

    /// <summary>Gets or sets the address of the uploaded <c>.woff2</c> file.</summary>
    public string? Src
    {
        get; set;
    }
}
