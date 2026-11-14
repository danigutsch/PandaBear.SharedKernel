namespace PandaBear.Blazor.Site.Theming;

/// <summary>The texts of <see cref="ThemePreview" />, in English by default, so the caller can localize them.</summary>
public sealed class ThemePreviewTexts
{
    /// <summary>Gets the accessible name of the preview region.</summary>
    public string Label { get; init; } = "Theme preview";

    /// <summary>Gets the sample level 1 heading.</summary>
    public string Heading1 { get; init; } = "Heading one";

    /// <summary>Gets the sample level 2 heading.</summary>
    public string Heading2 { get; init; } = "Heading two";

    /// <summary>Gets the sample level 3 heading.</summary>
    public string Heading3 { get; init; } = "Heading three";

    /// <summary>Gets the sample paragraph text before the link.</summary>
    public string Paragraph { get; init; } = "Body text of the site with";

    /// <summary>Gets the sample link text.</summary>
    public string Link { get; init; } = "a link";

    /// <summary>Gets the sample small print.</summary>
    public string Small { get; init; } = "Small print";

    /// <summary>Gets the primary button text.</summary>
    public string PrimaryButton { get; init; } = "Primary";

    /// <summary>Gets the secondary button text.</summary>
    public string SecondaryButton { get; init; } = "Secondary";

    /// <summary>Gets the sample card title.</summary>
    public string CardTitle { get; init; } = "Card";

    /// <summary>Gets the sample card text, shown in the muted colour.</summary>
    public string CardText { get; init; } = "Secondary text in a card.";

    /// <summary>Gets the sample navigation label.</summary>
    public string NavigationLabel { get; init; } = "Sample navigation";

    /// <summary>Gets the current sample navigation item.</summary>
    public string NavigationCurrent { get; init; } = "Home";

    /// <summary>Gets the other sample navigation item.</summary>
    public string NavigationOther { get; init; } = "Tours";

    /// <summary>Gets the caption of the contrast table; the scheme name follows in parentheses.</summary>
    public string ContrastCaption { get; init; } = "Contrast";

    /// <summary>Gets the header of the colour pair column.</summary>
    public string PairColumn { get; init; } = "Colours";

    /// <summary>Gets the header of the ratio column.</summary>
    public string RatioColumn { get; init; } = "Ratio";

    /// <summary>Gets the header of the required minimum column.</summary>
    public string RequiredColumn { get; init; } = "Required";

    /// <summary>Gets the header of the result column.</summary>
    public string ResultColumn { get; init; } = "Result";

    /// <summary>Gets the result text of a pair that reaches its minimum.</summary>
    public string Pass { get; init; } = "Pass";

    /// <summary>Gets the result text of a pair below its minimum.</summary>
    public string Fail { get; init; } = "Fail";

    /// <summary>Gets the name of the light scheme.</summary>
    public string LightScheme { get; init; } = "light";

    /// <summary>Gets the name of the dark scheme.</summary>
    public string DarkScheme { get; init; } = "dark";

    /// <summary>Gets the label of an error.</summary>
    public string Error { get; init; } = "Error";

    /// <summary>Gets the label of a warning.</summary>
    public string Warning { get; init; } = "Warning";

    /// <summary>Gets the word between the two colours of a pair.</summary>
    public string On { get; init; } = "on";
}
