using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;
using RouteData = Microsoft.AspNetCore.Components.RouteData;

namespace SharedKernel.Blazor.Site.Tests.CulturePages;

/// <summary>The root component of the culture page tests: a router over the test assembly's pages.</summary>
public sealed class CulturePagesRoot : ComponentBase
{
    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenComponent<Router>(0);
        builder.AddComponentParameter(1, nameof(Router.AppAssembly), typeof(CulturePagesRoot).Assembly);
        builder.AddComponentParameter(2, nameof(Router.Found), (RenderFragment<RouteData>)(routeData => route =>
        {
            route.OpenComponent<RouteView>(0);
            route.AddComponentParameter(1, nameof(RouteView.RouteData), routeData);
            route.CloseComponent();
        }));
        builder.CloseComponent();
    }
}
