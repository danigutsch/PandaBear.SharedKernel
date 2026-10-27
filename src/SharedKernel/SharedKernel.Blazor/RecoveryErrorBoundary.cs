using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor;

/// <summary>
/// An error boundary that reports a faulting child through <see cref="IUiFailureReporter" /> and renders only
/// caller-supplied fixed recovery copy.
/// </summary>
/// <remarks>
/// The exception message and stack never reach the page or, through this component, the log. Wrap the app's root
/// layout body with it so one component fault does not end the circuit. Requires an
/// <see cref="IUiFailureReporter" /> in the service provider; see <see cref="BlazorServiceCollectionExtensions.AddUiFailureReporting" />.
/// </remarks>
public sealed class RecoveryErrorBoundary : ErrorBoundaryBase
{
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
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (CurrentException is null)
        {
            builder.AddContent(0, ChildContent);
            return;
        }

        builder.OpenElement(1, "div");
        builder.AddAttribute(2, "role", "alert");
        builder.AddContent(3, RecoveryMessage);
        if (!string.IsNullOrWhiteSpace(RecoverLabel))
        {
            builder.OpenElement(4, "button");
            builder.AddAttribute(5, "type", "button");
            builder.AddAttribute(6, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, Recover));
            builder.AddContent(7, RecoverLabel);
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
