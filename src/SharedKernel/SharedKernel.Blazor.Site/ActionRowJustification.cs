namespace PandaBear.Blazor.Site;

/// <summary>
/// How an <see cref="ActionRow" /> places its actions in their row from the <c>48rem</c> breakpoint on, and on which side it stacks them
/// below it.
/// </summary>
public enum ActionRowJustification
{
    /// <summary>Together at the start of the row, where reading starts; stacked at the start below the breakpoint.</summary>
    Start = 0,

    /// <summary>Together in the middle of the row; stacked and centred below the breakpoint.</summary>
    Center = 1,

    /// <summary>Together at the end of the row; stacked at the end below the breakpoint.</summary>
    End = 2,

    /// <summary>
    /// Spread across the width, the first at the start and the last at the end, with the free space between them; stacked and centred
    /// below the breakpoint. Suits two actions that belong to opposite sides, such as previous and next.
    /// </summary>
    Between = 3,
}
