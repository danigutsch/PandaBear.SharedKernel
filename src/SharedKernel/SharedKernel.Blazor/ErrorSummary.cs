using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor;

/// <summary>
/// An accessible summary of form errors that links to each field and takes focus when new errors appear
/// (the GOV.UK error summary pattern).
/// </summary>
/// <remarks>
/// Renders a labelled <c>section</c> with <c>tabindex="-1"</c>, a heading, and a list of links to the fields. Each link is the current page address without its fragment plus
/// <c>#id</c>, so a document <c>&lt;base href&gt;</c> cannot send it elsewhere. The links carry
/// <c>target="_top"</c> so Blazor's link interception leaves them alone (it scrolls to a fragment without moving focus)
/// and the browser's native fragment navigation focuses the target, which must be an input or otherwise focusable. The caller
/// still shows a message beside each field (for example with <c>ValidationMessage</c>) and gives each field a stable id.
/// Messages are encoded and truncated to <see cref="MaxMessageLength" /> characters. Nothing is rendered without items. Focus moves when the <see cref="Items" /> list instance changes, so pass a new list
/// for every submission, including one with identical content, and keep the same list across unrelated re-renders.
/// Extra attributes, such as <c>class</c>, are applied to the section.
/// </remarks>
public sealed class ErrorSummary : ComponentBase
{
    /// <summary>The longest message shown before it is truncated.</summary>
    public const int MaxMessageLength = 300;

    private readonly string _titleId = "error-summary-title-" + Guid.NewGuid().ToString("N");
    private ElementReference _region;
    private IReadOnlyList<ErrorSummaryItem>? _focusedItems;
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

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary>A new list instance means a new submission, even with identical content, so it takes focus again; the same instance does not.</summary>
    private bool IsSameSubmission() => ReferenceEquals(Items, _focusedItems);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Title);
        ArgumentNullException.ThrowIfNull(Items);
        foreach (var item in Items)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(item.TargetId);
            ArgumentNullException.ThrowIfNull(item.Message);
        }

        if (IsSameSubmission())
        {
            return;
        }

        _focusedItems = Items;
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
        await FocusHelper.TryFocus(_region).ConfigureAwait(false);
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
        var pageAddress = Navigation.Uri;
        var fragmentStart = pageAddress.IndexOf('#', StringComparison.Ordinal);
        if (fragmentStart >= 0)
        {
            pageAddress = pageAddress[..fragmentStart];
        }

        builder.OpenElement(8, "ul");
        foreach (var item in Items)
        {
            builder.OpenElement(9, "li");
            builder.OpenElement(10, "a");
            builder.AddAttribute(11, "href", pageAddress + "#" + Uri.EscapeDataString(item.TargetId));
            builder.AddAttribute(12, "target", "_top");
            builder.AddContent(13, BoundedText.ToDisplay(item.Message, MaxMessageLength));
            builder.CloseElement();
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    }
}
