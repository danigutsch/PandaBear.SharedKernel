using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.Blazor.Site.Tests.CulturePages;

/// <summary>
/// A fixed section routed under both languages' segments, <c>/{culture}/viagens</c> and <c>/{culture}/tours</c>; each language shows it
/// only under its own segment.
/// </summary>
[Route("/{Culture}/viagens")]
[Route("/{Culture}/tours")]
public sealed class CultureSectionPage : SampleCulturePage
{
    /// <summary>The section path of a language: <c>/pt/viagens</c> or <c>/en/tours</c>.</summary>
    /// <param name="culture">The language.</param>
    /// <returns>The path.</returns>
    public static string Section(ContentCulture culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return culture.Value == "pt" ? "/pt/viagens" : "/en/tours";
    }

    /// <inheritdoc />
    protected override bool Load(CulturePageContent content) => InSection(Section);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddMarkupContent(0, Found ? "section" : string.Empty);
    }
}
