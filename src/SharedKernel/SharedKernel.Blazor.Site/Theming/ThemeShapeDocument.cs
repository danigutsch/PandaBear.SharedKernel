namespace SharedKernel.Blazor.Site.Theming;

/// <summary>
/// The shape of a <see cref="ThemeDocument" />, <see cref="SiteShape" />: the corners of controls and surfaces and the divider under section
/// headings and in the hero. A missing value keeps <see cref="SiteShape.Default" />'s.
/// </summary>
/// <param name="Control">The corner shape (<c>shape.control</c>).</param>
/// <param name="DividerWidth">The divider width in rem (<c>shape.dividerWidth</c>).</param>
/// <param name="DividerThickness">The divider thickness in rem (<c>shape.dividerThickness</c>).</param>
/// <param name="DividerColor">The divider colour role (<c>shape.dividerColor</c>): primary, accent, text, muted or border.</param>
public sealed record ThemeShapeDocument(
    ControlShape? Control = null,
    decimal? DividerWidth = null,
    decimal? DividerThickness = null,
    ThemeColorRole? DividerColor = null);
