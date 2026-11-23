namespace PandaBear.PageSections;

/// <summary>The button of a section: its text, where it links, and its look.</summary>
/// <typeparam name="TLink">The application's link target.</typeparam>
public sealed record PageLink<TLink>
    where TLink : notnull
{
    /// <summary>Initializes a button.</summary>
    /// <param name="text">The text.</param>
    /// <param name="target">The target.</param>
    /// <param name="style">The look.</param>
    internal PageLink(string text, TLink target, LinkStyle style)
    {
        Text = text;
        Target = target;
        Style = style;
    }

    /// <summary>Gets the button text.</summary>
    public string Text { get; }

    /// <summary>Gets where the button links, as the application's context validated it.</summary>
    public TLink Target { get; }

    /// <summary>Gets the look of the button.</summary>
    public LinkStyle Style { get; }
}
