using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using SharedKernel.Results;

namespace SharedKernel.Blazor.Tests;

/// <summary>
/// A list page that deletes rows with <see cref="ListDelete{TKey}"/> and captures its opener buttons, the safe Cancel button and the
/// status region with <c>@ref</c>, as the package README shows.
/// </summary>
internal sealed class ListDeletePage : ComponentBase
{
    private readonly ListDelete<int> _delete = new("We could not delete the row.");

    /// <summary>Gets the rows that are still shown.</summary>
    public List<int> Rows { get; } = [1, 2];

    /// <summary>Gets or sets the outcome of every deletion.</summary>
    [Parameter]
    public Result Outcome { get; set; } = Result.Ok();

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (_delete.Status is not null)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "role", "status");
            builder.AddAttribute(2, "tabindex", "-1");
            builder.AddElementReferenceCapture(3, element => _delete.StatusRegion = element);
            builder.AddContent(4, _delete.Status);
            builder.CloseElement();
        }

        builder.OpenElement(5, "ul");
        foreach (var row in Rows)
        {
            builder.OpenElement(6, "li");
            builder.SetKey(row);
            if (_delete.IsPending(row))
            {
                builder.OpenElement(7, "button");
                builder.AddAttribute(8, "id", $"confirm-{row}");
                builder.AddAttribute(9, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => Delete(row)));
                builder.AddContent(10, "Confirm delete");
                builder.CloseElement();
                builder.OpenElement(11, "button");
                builder.AddAttribute(12, "id", $"keep-{row}");
                builder.AddAttribute(13, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, _delete.Cancel));
                builder.AddElementReferenceCapture(14, element => _delete.SafeControl = element);
                builder.AddContent(15, "Cancel");
                builder.CloseElement();
            }
            else
            {
                builder.OpenElement(16, "button");
                builder.AddAttribute(17, "id", $"delete-{row}");
                builder.AddAttribute(18, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => _delete.Ask(row)));
                builder.AddElementReferenceCapture(19, element => _delete.Openers[row] = element);
                builder.AddContent(20, "Delete");
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => _delete.MoveFocus();

    private Task Delete(int row) => _delete.Run(row, () => ValueTask.FromResult(Outcome), () => Task.CompletedTask, key => Rows.Remove(key), $"Row {row} was deleted.");
}
