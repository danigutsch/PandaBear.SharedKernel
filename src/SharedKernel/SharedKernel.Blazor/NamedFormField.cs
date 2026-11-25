using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace SharedKernel.Blazor;

/// <summary>Renders a named validation field or group in the cascading edit context.</summary>
public sealed class NamedFormField : ComponentBase, IDisposable
{
    private EditContext? _subscribed;

    [CascadingParameter]
    private EditContext? CurrentEditContext { get; set; }

    /// <summary>Gets or sets the field identifier whose messages are rendered.</summary>
    [Parameter]
    [EditorRequired]
    public FieldIdentifier Field { get; set; }

    /// <summary>Gets or sets the control ID. Leave it null for a message-only group.</summary>
    [Parameter]
    public string? InputId { get; set; }

    /// <summary>Gets or sets the visible label.</summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>Gets or sets the optional help text displayed beneath the control.</summary>
    [Parameter]
    public string? Help { get; set; }

    /// <summary>Gets or sets a value indicating whether the label follows the control on the same line.</summary>
    [Parameter]
    public bool Inline { get; set; }

    /// <summary>Gets or sets existing description IDs to include in the control's accessibility attributes.</summary>
    [Parameter]
    public string? DescribedBy { get; set; }

    /// <summary>Gets or sets the control, which spreads the supplied ARIA attributes.</summary>
    [Parameter]
    public RenderFragment<IReadOnlyDictionary<string, object>>? ChildContent { get; set; }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">There is no cascading edit context.</exception>
    protected override void OnParametersSet()
    {
        var context = CurrentEditContext ?? throw new InvalidOperationException("NamedFormField requires a cascading EditContext.");
        if (ReferenceEquals(_subscribed, context))
        {
            return;
        }

        Dispose();
        _subscribed = context;
        _subscribed.OnValidationStateChanged += Refresh;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        string[] messages = _subscribed is null ? [] : [.. _subscribed.GetValidationMessages(Field)];
        FormFieldMarkup.Render(builder, InputId, Label, DescribedBy, Help, Inline, ChildContent, messages);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _subscribed?.OnValidationStateChanged -= Refresh;
        _subscribed = null;
    }

    private void Refresh(object? sender, ValidationStateChangedEventArgs args) => StateHasChanged();
}
