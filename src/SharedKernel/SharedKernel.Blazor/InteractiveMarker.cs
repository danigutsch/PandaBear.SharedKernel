using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor;

/// <summary>
/// A hidden element that reports whether the page is interactive: <c>data-interactive="false"</c> while it is only prerendered and
/// <c>"true"</c> once the interactive renderer (the Blazor Server circuit or WebAssembly) has taken over, as reported by
/// <c>RendererInfo.IsInteractive</c>. Browser tests wait for <see cref="ReadySelector"/> instead of sleeping, because clicks and typing
/// on a prerendered page are silently lost.
/// </summary>
public sealed class InteractiveMarker : ComponentBase
{
    /// <summary>The attribute that carries the state.</summary>
    public const string AttributeName = "data-interactive";

    /// <summary>A CSS selector that matches the marker once the page is interactive.</summary>
    public const string ReadySelector = "[data-interactive='true']";

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.OpenElement(0, "span");
        builder.AddAttribute(1, AttributeName, RendererInfo.IsInteractive ? "true" : "false");
        builder.AddAttribute(2, "hidden", true);
        builder.CloseElement();
    }
}
