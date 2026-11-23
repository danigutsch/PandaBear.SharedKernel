namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>The rendered box of an element in CSS pixels, rounded to a tenth of a pixel so sub-pixel layout noise compares equal.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
internal sealed record ElementBox(double X, double Y, double Width, double Height)
{
    /// <summary>Gets the bottom edge.</summary>
    public double Bottom => Math.Round(Y + Height, 1);

    /// <summary>Gets the right edge.</summary>
    public double Right => Math.Round(X + Width, 1);
}
