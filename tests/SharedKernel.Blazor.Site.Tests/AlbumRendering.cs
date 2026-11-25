using SharedKernel.BuildingBlocks;
using static SharedKernel.Blazor.Site.Tests.SiteFixtures;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Renders the album section for tests.</summary>
internal static class AlbumRendering
{
    /// <summary>Renders the album "Patagônia" (slug <c>patagonia</c>) with Portuguese pager copy and links to <c>/pt/galeria/patagonia</c>.</summary>
    /// <param name="context">The test context.</param>
    /// <param name="window">The page window.</param>
    /// <param name="photos">The photos of the page.</param>
    /// <param name="configure">Further parameters, applied last.</param>
    /// <returns>The rendered section.</returns>
    public static IRenderedComponent<AlbumSection> RenderAlbum(this BunitContext context, PageWindow window, GalleryImage[] photos,
        Action<ComponentParameterCollectionBuilder<AlbumSection>>? configure = null) =>
        context.Render<AlbumSection>(parameters =>
        {
            parameters
                .Add(album => album.Title, "Patagônia")
                .Add(album => album.Slug, Slug.Restore("patagonia"))
                .Add(album => album.Photos, photos)
                .Add(album => album.Window, window)
                .Add(album => album.PageHref, page => Link(page == 1 ? "/pt/galeria/patagonia" : $"/pt/galeria/patagonia?pagina={page}"))
                .Add(album => album.PagerLabel, "Páginas do álbum")
                .Add(album => album.PreviousText, "Anterior")
                .Add(album => album.NextText, "Próxima");
            configure?.Invoke(parameters);
        });

    /// <summary>Creates album summaries <c>Album 1</c> to <c>Album N</c> with decorative covers.</summary>
    /// <param name="count">The number of albums.</param>
    /// <returns>The summaries.</returns>
    public static AlbumSummary[] Summaries(int count) =>
        [.. Enumerable.Range(1, count).Select(index => new AlbumSummary($"Album {index}", Link($"/a{index}"), Image($"/c{index}.jpg", ImageAlternative.Decorative())))];
}
