using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SharedKernel.Blazor;

/// <summary>
/// The end of a form: the save error of its <see cref="ServerFormSubmission" /> as an alert, the submit button (disabled while saving),
/// any extra buttons, and the cancel link. Render it inside the <c>EditForm</c>, after the fields.
/// </summary>
public sealed class FormActions : ComponentBase
{
    /// <summary>Gets or sets the submission state of the form.</summary>
    [Parameter]
    [EditorRequired]
    public ServerFormSubmission? Submission { get; set; }

    /// <summary>Gets or sets the text of the submit button; <c>Save</c> by default.</summary>
    [Parameter]
    public string SubmitText { get; set; } = "Save";

    /// <summary>Gets or sets the address the cancel link leads to, or <see langword="null" /> for no cancel link.</summary>
    [Parameter]
    public string? CancelHref { get; set; }

    /// <summary>Gets or sets the text of the cancel link; <c>Cancel</c> by default.</summary>
    [Parameter]
    public string CancelText { get; set; } = "Cancel";

    /// <summary>Gets or sets extra buttons, shown between the submit button and the cancel link.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException"><see cref="Submission" /> is not set.</exception>
    protected override void OnParametersSet()
    {
        if (Submission is null)
        {
            throw new InvalidOperationException($"{nameof(FormActions)} requires a value for the '{nameof(Submission)}' parameter.");
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var submission = Submission!;
        if (submission.Error is { } error)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "role", "alert");
            builder.AddAttribute(2, "class", "error");
            builder.AddContent(3, error);
            builder.CloseElement();
        }

        builder.OpenElement(4, "p");
        builder.OpenElement(5, "button");
        builder.AddAttribute(6, "type", "submit");
        builder.AddAttribute(7, "disabled", submission.IsSaving);
        builder.AddContent(8, SubmitText);
        builder.CloseElement();
        builder.AddContent(9, ChildContent);
        if (CancelHref is not null)
        {
            builder.OpenElement(10, "a");
            builder.AddAttribute(11, "href", CancelHref);
            builder.AddContent(12, CancelText);
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
