using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor;

/// <summary>
/// One form field: a label tied to its control, the control itself, and the validation message of the bound field.
/// Render it inside an <c>EditForm</c>.
/// </summary>
/// <typeparam name="TValue">The type of the bound value.</typeparam>
public sealed class FormField<TValue> : ComponentBase
{
    /// <summary>Gets or sets the label text.</summary>
    [Parameter]
    [EditorRequired]
    public string Label
    {
        get; set;
    } = string.Empty;

    /// <summary>Gets or sets the HTML id of the control, which the label points at and error summary links target.</summary>
    [Parameter]
    [EditorRequired]
    public string InputId
    {
        get; set;
    } = string.Empty;

    /// <summary>Gets or sets the expression that selects the bound field, used to show its validation message.</summary>
    [Parameter]
    [EditorRequired]
    public Expression<Func<TValue>>? For
    {
        get; set;
    }

    /// <summary>Gets or sets the control; give it the id <see cref="InputId"/>.</summary>
    [Parameter]
    public RenderFragment? ChildContent
    {
        get; set;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.OpenElement(0, "label");
        builder.AddAttribute(1, "for", InputId);
        builder.AddContent(2, Label);
        builder.CloseElement();
        builder.AddContent(3, ChildContent);
        builder.OpenComponent<ValidationMessage<TValue>>(4);
        builder.AddComponentParameter(5, nameof(ValidationMessage<TValue>.For), For);
        builder.CloseComponent();
    }
}
