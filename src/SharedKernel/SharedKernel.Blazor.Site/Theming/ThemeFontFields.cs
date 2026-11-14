using System.Collections.ObjectModel;

namespace PandaBear.Blazor.Site.Theming;

/// <summary>The editor fields of one self-hosted font.</summary>
public sealed class ThemeFontFields
{
    /// <summary>Gets or sets the key that typography roles reference.</summary>
    public string? Key { get; set; }

    /// <summary>Gets or sets the family name.</summary>
    public string? Family { get; set; }

    /// <summary>Gets the faces.</summary>
    public Collection<ThemeFontFaceFields> Faces { get; } = [];
}
