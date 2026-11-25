namespace SharedKernel.Blazor.Site.Theming;

/// <summary>The editor fields of one colour token; both empty on a hover colour means derived.</summary>
public sealed class ThemeColorFields
{
    /// <summary>Gets or sets the light value, such as <c>#1d4ed8</c>.</summary>
    public string? Light { get; set; }

    /// <summary>Gets or sets the dark value.</summary>
    public string? Dark { get; set; }
}
