using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SharedKernel.Blazor.Site.Tests.CulturePages;

/// <summary>A page at <c>/{culture}/culture-sample</c> that always exists and writes its language and the content's language.</summary>
[Route("/{Culture}/culture-sample")]
public sealed class CultureSamplePage : SampleCulturePage
{
    /// <inheritdoc />
    protected override bool Load(CulturePageContent content) => true;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (Found)
        {
            builder.AddMarkupContent(0, $"<p>language={Language.Value};content={Content!.Culture.Value};path={CurrentPath}</p>");
        }
    }
}
