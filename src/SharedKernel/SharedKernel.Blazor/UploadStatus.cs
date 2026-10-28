using System.Globalization;
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
/// steps throttle what assistive technology reads. When the cancel button had focus or was activated and disappears, focus
/// moves to the region. The component does not cancel transport work, and it renders when the parent re-renders after mutating the
/// state. Focus changes on the button are tracked without re-rendering.
/// File names and failure messages are encoded and truncated to <see cref="MaxDisplayTextLength" /> characters.
/// An optional <see cref="Detail" /> fragment renders visible, non-live caller content after the status region; the
/// live region keeps announcing only the throttled announcement.
/// The accessible-name and progress-text formats, the class parameters, and <see cref="RenderFailureInStatus" /> let a
/// consumer match existing copy and styling; unset, the markup is as described above. Formats receive only the bounded
/// file name and byte counts, and the component names no CSS framework.
/// Extra attributes, such as <c>class</c>, are applied to the section.
/// </remarks>
public sealed class UploadStatus : ComponentBase, IHandleEvent
{
    /// <summary>The longest file name or failure message shown before it is truncated.</summary>
    public const int MaxDisplayTextLength = 200;

    private ElementReference _region;
    private bool _cancelRendered;
    private bool _cancelFocused;
    private bool _cancelActivated;

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
    /// Gets or sets a composite format for the cancel button's accessible name, where <c>{0}</c> is the bounded file name,
    /// for example <c>Cancel upload of {0}</c>. When <see langword="null" />, the name is <see cref="CancelText" />, a colon,
    /// and the file name. The visible button text stays <see cref="CancelText" />.
    /// </summary>
    [Parameter]
    public string? CancelLabelFormat
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets a composite format for the determinate progress element's accessible name, where <c>{0}</c> is the
    /// bounded file name. When <see langword="null" />, the name is <see cref="ProgressLabel" />, a colon, and the file name.
    /// </summary>
    [Parameter]
    public string? ProgressLabelFormat
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets a composite format for the indeterminate progress element's accessible name, where <c>{0}</c> is the
    /// bounded file name. When <see langword="null" />, the determinate rule applies, so one label serves both.
    /// </summary>
    [Parameter]
    public string? IndeterminateProgressLabelFormat
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets a composite format for the determinate progress element's fallback content, where <c>{0}</c> is the
    /// bytes transferred and <c>{1}</c> the total, for example <c>{0} of {1} bytes</c>. When <see langword="null" />, the
    /// content is <c>{0} / {1}</c>.
    /// </summary>
    [Parameter]
    public string? ProgressTextFormat
    {
        get; set;
    }

    /// <summary>Gets or sets the CSS class of the cancel button; no <c>class</c> attribute is rendered when <see langword="null" />.</summary>
    [Parameter]
    public string? CancelClass
    {
        get; set;
    }

    /// <summary>Gets or sets the CSS class of the progress element; no <c>class</c> attribute is rendered when <see langword="null" />.</summary>
    [Parameter]
    public string? ProgressClass
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets the CSS class of the <c>role="status"</c> element, for example to hide it visually while it is still
    /// announced; no <c>class</c> attribute is rendered when <see langword="null" />.
    /// </summary>
    [Parameter]
    public string? StatusClass
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets whether a failed state's message is rendered inside the live region, <see langword="true" /> by
    /// default. When <see langword="false" />, the live region holds only the throttled announcement and the caller shows
    /// the failure, for example in <see cref="Detail" />.
    /// </summary>
    [Parameter]
    public bool RenderFailureInStatus { get; set; } = true;

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
        var moveFocus = _cancelRendered && !cancelVisible && (_cancelFocused || _cancelActivated);
        _cancelRendered = cancelVisible;
        if (!cancelVisible)
        {
            _cancelFocused = false;
            _cancelActivated = false;
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
        builder.AddAttribute(7, "class", StatusClass);
        builder.AddContent(8, BoundedText.ToDisplay(State.Announcement, MaxDisplayTextLength * 2));
        if (RenderFailureInStatus && State.Status is UploadOperationStatus.Failed && !string.IsNullOrWhiteSpace(State.FailureMessage))
        {
            builder.AddContent(9, " ");
            builder.OpenElement(10, "span");
            builder.AddAttribute(11, "data-upload-failure", true);
            builder.AddContent(12, BoundedText.ToDisplay(State.FailureMessage, MaxDisplayTextLength));
            builder.CloseElement();
        }

        builder.CloseElement();

        if (Detail is not null)
        {
            builder.OpenElement(13, "div");
            builder.AddAttribute(14, "data-upload-detail", true);
            builder.AddContent(15, Detail(State));
            builder.CloseElement();
        }

        if (State.IsActive)
        {
            BuildProgress(builder, fileName);
            BuildCancelButton(builder, fileName);
        }

        builder.CloseElement();
    }

    private string ProgressAccessibleName(string fileName)
    {
        var format = State.IsDeterminate ? ProgressLabelFormat : IndeterminateProgressLabelFormat ?? ProgressLabelFormat;
        return AccessibleName(format, ProgressLabel, fileName);
    }

    private static string AccessibleName(string? format, string prefix, string fileName)
        => format is null ? $"{prefix}: {fileName}" : string.Format(CultureInfo.InvariantCulture, format, fileName);

    private void BuildProgress(RenderTreeBuilder builder, string fileName)
    {
        builder.OpenElement(20, "progress");
        builder.AddAttribute(21, "aria-label", ProgressAccessibleName(fileName));
        builder.AddAttribute(22, "class", ProgressClass);
        if (State.IsDeterminate)
        {
            builder.AddAttribute(23, "max", State.TotalBytes);
            builder.AddAttribute(24, "value", State.BytesTransferred);
            builder.AddContent(25, string.Format(CultureInfo.InvariantCulture, ProgressTextFormat ?? "{0} / {1}", State.BytesTransferred, State.TotalBytes));
        }

        builder.CloseElement();
    }

    private Task ActivateCancel()
    {
        _cancelActivated = true;
        return OnCancel.InvokeAsync();
    }

    /// <remarks>
    /// The focus handlers are plain delegates: they have no receiver, so tracking focus does not re-render the component.
    /// A browser blurs a focused element it removes, and that event can reach the server before the render is acknowledged,
    /// so a blur is a user moving focus away only while the state is still active; otherwise it is the removal itself.
    /// Activating the button also marks it for the focus move, because Safari does not focus a button on mouse click.
    /// </remarks>
    private void BuildCancelButton(RenderTreeBuilder builder, string fileName)
    {
        builder.OpenElement(30, "button");
        builder.AddAttribute(31, "type", "button");
        builder.AddAttribute(32, "aria-label", AccessibleName(CancelLabelFormat, CancelText, fileName));
        builder.AddAttribute(33, "class", CancelClass);
        builder.AddAttribute(34, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, ActivateCancel));

        builder.AddAttribute(35, "onfocus", (Action<FocusEventArgs>)(_ => _cancelFocused = true));
        builder.AddAttribute(36, "onblur", (Action<FocusEventArgs>)(_ =>
        {
            _cancelFocused &= !State.IsActive;
            _cancelActivated &= !State.IsActive;
        }));
        builder.AddContent(37, CancelText);
        builder.CloseElement();
    }
}
