using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>A table of label and value rows with a caption and <c>th scope="row"</c> row headers.</summary>
public sealed partial class FactsTable
{
    /// <summary>Gets or sets the caption, which names the table for assistive technology.</summary>
    [Parameter]
    [EditorRequired]
    public string Caption { get; set; } = string.Empty;

    /// <summary>Gets or sets the rows.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<FactRow> Rows { get; set; } = [];

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Caption);
        ArgumentNullException.ThrowIfNull(Rows);
    }
}
