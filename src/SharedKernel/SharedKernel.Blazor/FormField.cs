using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace SharedKernel.Blazor;

/// <summary>
/// One form field: a label tied to its control, the control itself, and the validation messages of the bound field, which describe
/// the control. Render it inside an <c>EditForm</c>.
/// </summary>
/// <remarks>
/// Each message is a <c>div class="validation-message"</c> with a stable id: <c>{InputId}-error</c> for the first, then
/// <c>{InputId}-error-2</c> and so on. The control receives its accessibility attributes as the context of <see cref="ChildContent"/>
/// and spreads them with <c>@attributes="context"</c>: <c>aria-describedby</c> lists <see cref="DescribedBy"/> and then the message ids,
/// and <c>aria-invalid="true"</c> is set while the field has a message. A valid field without <see cref="DescribedBy"/> gets neither.
/// The field re-renders when the validation state of the edit context changes.
/// </remarks>
/// <typeparam name="TValue">The type of the bound value.</typeparam>
public sealed class FormField<TValue> : ComponentBase, IDisposable
{
    private EditContext? _subscribed;
    private Expression<Func<TValue>>? _for;
    private FieldIdentifier _field;

    /// <summary>Gets or sets the label text.</summary>
    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the HTML id of the control, which the label points at, error summary links target, and the message ids start with.</summary>
    [Parameter]
    [EditorRequired]
    public string InputId { get; set; } = string.Empty;

    /// <summary>Gets or sets the expression that selects the bound field, whose validation messages are shown.</summary>
    [Parameter]
    [EditorRequired]
    public Expression<Func<TValue>>? For { get; set; }

    /// <summary>
    /// Gets or sets the ids of other elements that describe the control, such as a help text, separated by spaces. They come first in
    /// the control's <c>aria-describedby</c>, also while the field is valid. Pass them here instead of setting <c>aria-describedby</c> on the
    /// control, which would replace the attribute the field hands it.
    /// </summary>
    [Parameter]
    public string? DescribedBy { get; set; }

    /// <summary>
    /// Gets or sets the control; give it the id <see cref="InputId"/> and spread the context, the field's attributes for the control, with
    /// <c>@attributes="context"</c>. Inside an <c>EditForm</c> whose child content also uses the implicit <c>context</c> name, rename one of
    /// them with the <c>Context</c> attribute (for example <c>&lt;EditForm Context="form"&gt;</c>).
    /// </summary>
    [Parameter]
    public RenderFragment<IReadOnlyDictionary<string, object>>? ChildContent { get; set; }

    [CascadingParameter]
    private EditContext? CurrentEditContext { get; set; }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The field is not inside an <c>EditForm</c>, or <see cref="For"/> is not set.</exception>
    protected override void OnParametersSet()
    {
        if (CurrentEditContext is null)
        {
            throw new InvalidOperationException($"{nameof(FormField<TValue>)} requires a cascading {nameof(EditContext)}; render it inside an EditForm.");
        }

        if (For is null)
        {
            throw new InvalidOperationException($"{nameof(FormField<TValue>)} requires a value for the '{nameof(For)}' parameter.");
        }

        if (!ReferenceEquals(For, _for))
        {
            _for = For;
            _field = FieldIdentifier.Create(For);
        }

        if (!ReferenceEquals(CurrentEditContext, _subscribed))
        {
            Unsubscribe();
            _subscribed = CurrentEditContext;
            _subscribed.OnValidationStateChanged += Refresh;
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        string[] messages = _subscribed is null ? [] : [.. _subscribed.GetValidationMessages(_field)];
        FormFieldMarkup.Render(builder, InputId, Label, DescribedBy, null, false, ChildContent, messages);
    }

    /// <inheritdoc />
    public void Dispose() => Unsubscribe();

    private void Refresh(object? sender, ValidationStateChangedEventArgs e) => StateHasChanged();

    private void Unsubscribe() => _subscribed?.OnValidationStateChanged -= Refresh;
}
