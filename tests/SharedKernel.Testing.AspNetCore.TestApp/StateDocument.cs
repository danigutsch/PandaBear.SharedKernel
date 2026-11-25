using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace SharedKernel.Testing.AspNetCore.TestApp;

/// <summary>A prerendered page with one Interactive Server component whose persisted state has the size the query asks for.</summary>
[Route("/state")]
public sealed class StateDocument : ComponentBase
{
    /// <summary>Gets or sets the number of characters the interactive component persists.</summary>
    [SupplyParameterFromQuery]
    public int Size { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddMarkupContent(0, "<!DOCTYPE html>");
        builder.OpenElement(1, "html");
        builder.OpenElement(2, "body");
        builder.OpenComponent<StateHolder>(3);
        builder.AddComponentParameter(4, nameof(StateHolder.Size), Size);
        builder.AddComponentRenderMode(RenderMode.InteractiveServer);
        builder.CloseComponent();
        builder.CloseElement();
        builder.CloseElement();
    }
}
