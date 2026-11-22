using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A row of actions, such as the buttons that close a page: a plain <c>div</c>, never a landmark, that keeps its children in reading
/// order. From the <c>48rem</c> breakpoint on they are one wrapping row placed by <see cref="Justification" />, together at the start by
/// default; below it they are stacked on the same side, or centred. A <see cref="ButtonLink" /> keeps its button look and a plain link
/// stays a link in the primary colour.
/// </summary>
public sealed partial class ActionRow
{
    /// <summary>Gets or sets the actions, such as <see cref="ButtonLink" /> components and links.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets how the actions are placed in their row. The default is <see cref="ActionRowJustification.Start" />: the actions stay
    /// together where reading starts, in line with the text above them, so the eye finds them as one group;
    /// <see cref="ActionRowJustification.Center" />, <see cref="ActionRowJustification.End" /> and
    /// <see cref="ActionRowJustification.Between" /> add <c>site-action-row--center</c>, <c>site-action-row--end</c> and
    /// <c>site-action-row--between</c>.
    /// </summary>
    [Parameter]
    public ActionRowJustification Justification { get; set; }

    private string RootClass => Justification switch
    {
        ActionRowJustification.Center => "site-action-row site-action-row--center",
        ActionRowJustification.End => "site-action-row site-action-row--end",
        ActionRowJustification.Between => "site-action-row site-action-row--between",
        _ => "site-action-row",
    };

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!Enum.IsDefined(Justification))
        {
            throw new ArgumentOutOfRangeException(nameof(Justification), Justification, "The action row justification is not defined.");
        }
    }
}
