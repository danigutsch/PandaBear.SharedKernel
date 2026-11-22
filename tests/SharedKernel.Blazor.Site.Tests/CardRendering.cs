namespace PandaBear.Blazor.Site.Tests;

/// <summary>Renders the cards and button links that the card option tests share.</summary>
internal static class CardRendering
{
    /// <summary>Renders the card "Atacama", linked to <c>/tours/atacama</c> with a 400 by 300 image, then applies further parameters.</summary>
    /// <param name="context">The test context.</param>
    /// <param name="configure">Further parameters, applied last.</param>
    /// <returns>The rendered card.</returns>
    public static IRenderedComponent<Card> RenderCard(this BunitContext context, Action<ComponentParameterCollectionBuilder<Card>>? configure = null) =>
        context.Render<Card>(parameters =>
        {
            parameters
                .Add(component => component.Title, "Atacama")
                .Add(component => component.Href, SiteFixtures.Link("/tours/atacama"))
                .Add(component => component.Image, SiteFixtures.Image("/atacama.jpg"));
            configure?.Invoke(parameters);
        });

    /// <summary>Renders a primary button link "Go" to <c>/tours</c> of a size.</summary>
    /// <param name="context">The test context.</param>
    /// <param name="size">The size.</param>
    /// <returns>The rendered link.</returns>
    public static IRenderedComponent<ButtonLink> RenderButton(this BunitContext context, ButtonSize size) => context.Render<ButtonLink>(parameters => parameters
        .Add(component => component.Href, SiteFixtures.Link("/tours"))
        .Add(component => component.Size, size)
        .AddChildContent("Go"));
}
