using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SharedKernel.Blazor.Site.Tests.Methods;

/// <summary>The root component and a page at <c>/methods</c> of the method tests; it renders <c>page</c>.</summary>
[Route("/methods")]
public sealed class MethodsPage : ComponentBase
{
    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddMarkupContent(0, "page");
    }
}
