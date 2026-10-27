using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace PandaBear.Blazor;

/// <summary>
/// An error boundary that reports a faulting child through <see cref="IUiFailureReporter" /> and renders only
/// caller-supplied fixed recovery copy.
/// </summary>
/// <remarks>
/// The exception message and stack never reach the page or, through this component, the log. Wrap the app's root
/// layout body with it so one component fault does not end the circuit. Requires an
/// <see cref="IUiFailureReporter" /> in the service provider; see <see cref="BlazorServiceCollectionExtensions.AddUiFailureReporting" />.
/// After a fault, focus moves to the alert. After the recover button renders the child again, focus moves to an empty
/// <c>tabindex="-1"</c> anchor placed before the child's content, so keyboard focus is not lost with the removed button.
/// </remarks>
public sealed class RecoveryErrorBoundary : ErrorBoundaryBase
{
    private ElementReference _alert;
    private ElementReference _anchor;
    private bool _focusAlert;
    private bool _focusAnchor;
    private bool _showAnchor;

    [Inject]
    private IUiFailureReporter Reporter { get; set; } = default!;

    /// <summary>Gets or sets the fixed copy shown after a fault. Rendered in a <c>role="alert"</c> element.</summary>
    [Parameter]
    [EditorRequired]
    public string RecoveryMessage { get; set; } = string.Empty;

    /// <summary>Gets or sets the stable operation name logged with the failure, "ui.error-boundary" by default.</summary>
    [Parameter]
    public string OperationName { get; set; } = "ui.error-boundary";

    /// <summary>Gets or sets the label of a button that clears the fault and renders the child again; no button when null or blank.</summary>
    [Parameter]
    public string? RecoverLabel
    {
        get; set;
    }

    /// <inheritdoc />
    protected override Task OnErrorAsync(Exception exception)
    {
        Reporter.Report(exception, OperationName);
        _focusAlert = true;
        _focusAnchor = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusAlert && CurrentException is not null)
        {
            _focusAlert = false;
            await FocusHelper.TryFocus(_alert).ConfigureAwait(false);
        }
        else if (_focusAnchor && CurrentException is null)
        {
            _focusAnchor = false;
            await FocusHelper.TryFocus(_anchor).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (CurrentException is null)
        {
            if (_showAnchor)
            {
                builder.OpenElement(0, "span");
                builder.AddAttribute(1, "data-recovery-anchor", true);
                builder.AddAttribute(2, "tabindex", "-1");
                builder.AddElementReferenceCapture(3, anchor => _anchor = anchor);
                builder.CloseElement();
            }

            builder.AddContent(4, ChildContent);
            return;
        }

        builder.OpenElement(5, "div");
        builder.AddAttribute(6, "role", "alert");
        builder.AddAttribute(7, "tabindex", "-1");
        builder.AddElementReferenceCapture(8, alert => _alert = alert);
        builder.AddContent(9, RecoveryMessage);
        if (!string.IsNullOrWhiteSpace(RecoverLabel))
        {
            builder.OpenElement(10, "button");
            builder.AddAttribute(11, "type", "button");
            builder.AddAttribute(12, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, RecoverAndFocus));
            builder.AddContent(13, RecoverLabel);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void RecoverAndFocus()
    {
        _showAnchor = true;
        _focusAnchor = true;
        Recover();
    }
}
