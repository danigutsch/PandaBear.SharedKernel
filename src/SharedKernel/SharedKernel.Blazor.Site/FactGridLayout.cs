namespace SharedKernel.Blazor.Site;

/// <summary>How a <see cref="FactGrid" /> lays out its cards.</summary>
public enum FactGridLayout
{
    /// <summary>
    /// Centred cards bordered like the cells of a table, in balanced rows of at most four from the <c>48rem</c> breakpoint on, such as a
    /// tour's basic information.
    /// </summary>
    Grid = 0,

    /// <summary>
    /// One row of compact facts without borders (<c>site-fact-grid--inline</c>), each its icon and label above its value, that wraps
    /// only when the row is too narrow, such as the days, distance and difficulty on a tour's card.
    /// </summary>
    Inline = 1,
}
