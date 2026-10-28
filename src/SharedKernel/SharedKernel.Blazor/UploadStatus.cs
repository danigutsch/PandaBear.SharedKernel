using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using PandaBear.HttpClients;

namespace PandaBear.Blazor;

/// <summary>
/// An accessible view of an <see cref="UploadOperationState" />: a labelled status region, progress, and a cancel button.
/// </summary>
/// <remarks>
/// The component always renders a <c>section</c> named by <see cref="Label" /> with <c>tabindex="-1"</c> and an
/// initially empty <c>role="status"</c> element (an implicit polite, atomic live region), so assistive technology
/// already knows the region when the first announcement arrives. The status holds the state's announcement and, after
/// a failure, the failure reason. A determinate <c>progress</c> only while the state
/// is uploading and an indeterminate one while it is otherwise active, and a cancel button named after the file.
/// The announcement changes only when <see cref="UploadOperationState.Announcement" /> does, so the state's ten-percent
/// steps throttle what assistive technology reads. When the cancel button had focus and disappears, focus moves to the
/// region. The component does not cancel transport work, and it renders when the parent re-renders after mutating the
/// state. Focus changes on the button are tracked without re-rendering.
/// File names and failure messages are encoded and truncated to <see cref="MaxDisplayTextLength" /> characters.
/// An optional <see cref="Detail" /> fragment renders visible, non-live caller content after the status region; the
/// live region keeps announcing only the throttled announcement.
/// Extra attributes, such as <c>class</c>, are applied to the section.
/// </remarks>
public sealed class UploadStatus : ComponentBase, IHandleEvent
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

    /// <summary>
    /// Gets or sets caller-supplied visible status detail, such as exact byte counts or a retry hint. It renders in a
    /// non-live <c>div data-upload-detail</c> after the status region, so it is never announced. The caller bounds and
    /// encodes its own text.
    /// </summary>
    [Parameter]
    public RenderFragment<UploadOperationState>? Detail
    {
        get; set;
    }

    /// <summary>Gets or sets additional attributes applied to the section.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes
    {
        get; set;
    }

    /// <summary>Handles element events without re-rendering, so focus changes on the cancel button cost no render.</summary>
    /// <param name="callback">The event handler.</param>
    /// <param name="arg">The event arguments.</param>
    /// <returns>A task that completes when the handler does.</returns>
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg) => callback.InvokeAsync(arg);

    /// <inheritdoc />
    protected override void OnParametersSet() => ArgumentNullException.ThrowIfNull(State);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var cancelVisible = State.IsActive;
        var moveFocus = _cancelRendered && !cancelVisible && _cancelFocused;
        _cancelRendered = cancelVisible;
        if (!cancelVisible)
        {
            _cancelFocused = false;
        }

        if (!moveFocus)
        {
            return;
        }

        await FocusHelper.TryFocus(_region).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var fileName = BoundedText.ToDisplay(State.FileName, MaxDisplayTextLength);
        builder.OpenElement(0, "section");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "aria-label", Label);
        builder.AddAttribute(3, "tabindex", "-1");
        builder.AddElementReferenceCapture(4, region => _region = region);

        builder.OpenElement(5, "div");
        builder.AddAttribute(6, "role", "status");
        builder.AddContent(7, BoundedText.ToDisplay(State.Announcement, MaxDisplayTextLength * 2));
        if (State.Status is UploadOperationStatus.Failed && !string.IsNullOrWhiteSpace(State.FailureMessage))
        {
            builder.AddContent(8, " ");
            builder.OpenElement(9, "span");
            builder.AddAttribute(10, "data-upload-failure", true);
            builder.AddContent(11, BoundedText.ToDisplay(State.FailureMessage, MaxDisplayTextLength));
            builder.CloseElement();
        }

        builder.CloseElement();

        if (Detail is not null)
        {
            builder.OpenElement(12, "div");
            builder.AddAttribute(13, "data-upload-detail", true);
            builder.AddContent(14, Detail(State));
            builder.CloseElement();
        }

        if (State.IsActive)
        {
            BuildProgress(builder, fileName);
            BuildCancelButton(builder, fileName);
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

        // Plain delegates have no receiver, so tracking focus does not re-render the component.
        builder.AddAttribute(34, "onfocus", (Action<FocusEventArgs>)(_ => _cancelFocused = true));
        builder.AddAttribute(35, "onblur", (Action<FocusEventArgs>)(_ => _cancelFocused = false));
        builder.AddContent(36, CancelText);
        builder.CloseElement();
    }
}
