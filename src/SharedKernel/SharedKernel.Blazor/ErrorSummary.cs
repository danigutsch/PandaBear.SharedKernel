using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace PandaBear.Blazor;

/// <summary>
/// An accessible summary of form errors that links to each field and takes focus when new errors appear
/// (the GOV.UK error summary pattern).
/// </summary>
/// <remarks>
/// Renders a labelled <c>section</c> with <c>tabindex="-1"</c>, a heading, and a list of fragment links. The caller
/// still shows a message beside each field (for example with <c>ValidationMessage</c>) and gives each field a stable id.
/// Messages are encoded and truncated to <see cref="MaxMessageLength" /> characters. Nothing is rendered without items.
/// Extra attributes, such as <c>class</c>, are applied to the section.
/// </remarks>
public sealed class ErrorSummary : ComponentBase
{
    /// <summary>The longest message shown before it is truncated.</summary>
    public const int MaxMessageLength = 300;

    private readonly string _titleId = "error-summary-title-" + Guid.NewGuid().ToString("N");
    private ElementReference _region;
    private IReadOnlyList<ErrorSummaryItem> _focusedItems = [];
    private bool _focusPending;

    /// <summary>Gets or sets the heading, for example "There is a problem". Caller-supplied fixed copy.</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the entries to show.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<ErrorSummaryItem> Items { get; set; } = [];

    /// <summary>Gets or sets additional attributes applied to the section.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes
    {
        get; set;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Items.SequenceEqual(_focusedItems))
        {
            return;
        }

        _focusedItems = [.. Items];
        _focusPending = Items.Count > 0;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusPending)
        {
            return;
        }

        _focusPending = false;
        try
        {
            await _region.FocusAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; there is nothing left to focus.
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (Items.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "section");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "tabindex", "-1");
        builder.AddAttribute(3, "aria-labelledby", _titleId);
        builder.AddElementReferenceCapture(4, region => _region = region);
        builder.OpenElement(5, "h2");
        builder.AddAttribute(6, "id", _titleId);
        builder.AddContent(7, BoundedText.ToDisplay(Title, MaxMessageLength));
        builder.CloseElement();
        builder.OpenElement(8, "ul");
        foreach (var item in Items)
        {
            builder.OpenElement(9, "li");
            builder.OpenElement(10, "a");
            builder.AddAttribute(11, "href", "#" + Uri.EscapeDataString(item.TargetId));
            builder.AddContent(12, BoundedText.ToDisplay(item.Message, MaxMessageLength));
            builder.CloseElement();
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    }
}
