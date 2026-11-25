using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SharedKernel.Blazor.Site.Tests.CulturePages;

/// <summary>A page at <c>/{culture}/culture-missing</c> whose item never exists in the content.</summary>
[Route("/{Culture}/culture-missing")]
public sealed class CultureMissingPage : SampleCulturePage
{
    /// <inheritdoc />
    protected override bool Load(CulturePageContent content) => false;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddMarkupContent(0, Found ? "found" : string.Empty);
    }
}
