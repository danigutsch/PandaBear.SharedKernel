using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using PandaBear.HttpClients;

namespace PandaBear.Blazor;

/// <summary>
/// An accessible view of an <see cref="UploadOperationState" />: a labelled status region, progress, and a cancel button.
/// </summary>
/// <remarks>
/// The component renders a <c>section</c> named by <see cref="Label" /> with <c>tabindex="-1"</c>, a polite, atomic
/// <c>role="status"</c> element holding the state's announcement, a determinate <c>progress</c> only while the state
/// is uploading and an indeterminate one while it is otherwise active, and a cancel button named after the file.
/// The announcement changes only when <see cref="UploadOperationState.Announcement" /> does, so the state's ten-percent
/// steps throttle what assistive technology reads. When the cancel button had focus and disappears, focus moves to the
/// region. The component does not cancel transport work, and it renders when the parent re-renders after mutating the
/// state. Reset the state only after moving focus yourself, because the region disappears with the state.
/// File names and failure messages are encoded and truncated to <see cref="MaxDisplayTextLength" /> characters.
/// Extra attributes, such as <c>class</c>, are applied to the section. Nothing is rendered while the state is
/// <see cref="UploadOperationStatus.None" />.
/// </remarks>
public sealed class UploadStatus : ComponentBase
{
    /// <summary>The longest file name or failure message shown before it is truncated.</summary>
    public const int MaxDisplayTextLength = 200;

    private ElementReference _region;
    private bool _cancelRendered;
    private bool _cancelFocused;

    /// <summary>Gets or sets the state to show.</summary>
    [Parameter]
    [EditorRequired]
    public UploadOperationState State { get; set; } = null!;

    /// <summary>Gets or sets the accessible name of the status region. Caller-supplied fixed copy.</summary>
    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the callback raised when the cancel button is activated.</summary>
    [Parameter]
    public EventCallback OnCancel
    {
        get; set;
    }

    /// <summary>Gets or sets the cancel button text, "Cancel upload" by default; the accessible name appends the file name.</summary>
    [Parameter]
    public string CancelText { get; set; } = "Cancel upload";

    /// <summary>Gets or sets the progress element's accessible name prefix, "Upload progress" by default.</summary>
    [Parameter]
    public string ProgressLabel { get; set; } = "Upload progress";

    /// <summary>Gets or sets additional attributes applied to the section.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes
    {
        get; set;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var cancelVisible = State.IsActive;
        var moveFocus = _cancelRendered && !cancelVisible && _cancelFocused && State.Status is not UploadOperationStatus.None;
        _cancelRendered = cancelVisible;
        if (!cancelVisible)
        {
            _cancelFocused = false;
        }

        if (!moveFocus)
        {
            return;
        }

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

        if (State.Status is UploadOperationStatus.None)
        {
            return;
        }

        var fileName = BoundedText.ToDisplay(State.FileName, MaxDisplayTextLength);
        builder.OpenElement(0, "section");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "aria-label", Label);
        builder.AddAttribute(3, "tabindex", "-1");
        builder.AddElementReferenceCapture(4, region => _region = region);

        builder.OpenElement(5, "div");
        builder.AddAttribute(6, "role", "status");
        builder.AddAttribute(7, "aria-live", "polite");
        builder.AddAttribute(8, "aria-atomic", "true");
        builder.AddContent(9, BoundedText.ToDisplay(State.Announcement, MaxDisplayTextLength * 2));
        builder.CloseElement();

        if (State.IsActive)
        {
            BuildProgress(builder, fileName);
            BuildCancelButton(builder, fileName);
        }

        if (State.Status is UploadOperationStatus.Failed && !string.IsNullOrWhiteSpace(State.FailureMessage))
        {
            builder.OpenElement(10, "p");
            builder.AddAttribute(11, "data-upload-failure", true);
            builder.AddContent(12, BoundedText.ToDisplay(State.FailureMessage, MaxDisplayTextLength));
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void BuildProgress(RenderTreeBuilder builder, string fileName)
    {
        builder.OpenElement(20, "progress");
        builder.AddAttribute(21, "aria-label", $"{ProgressLabel}: {fileName}");
        if (State.IsDeterminate)
        {
            builder.AddAttribute(22, "max", State.TotalBytes);
            builder.AddAttribute(23, "value", State.BytesTransferred);
            builder.AddContent(24, $"{State.BytesTransferred} / {State.TotalBytes}");
        }

        builder.CloseElement();
    }

    private void BuildCancelButton(RenderTreeBuilder builder, string fileName)
    {
        builder.OpenElement(30, "button");
        builder.AddAttribute(31, "type", "button");
        builder.AddAttribute(32, "aria-label", $"{CancelText}: {fileName}");
        builder.AddAttribute(33, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => OnCancel.InvokeAsync()));
        builder.AddAttribute(34, "onfocus", EventCallback.Factory.Create<FocusEventArgs>(this, () => _cancelFocused = true));
        builder.AddAttribute(35, "onblur", EventCallback.Factory.Create<FocusEventArgs>(this, () => _cancelFocused = false));
        builder.AddContent(36, CancelText);
        builder.CloseElement();
    }
}
