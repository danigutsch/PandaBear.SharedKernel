using System.Globalization;
using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Renders the pager for tests.</summary>
internal static class PagerRendering
{
    /// <summary>Renders a pager whose links point at <c>/fotos</c> for page 1 and <c>/fotos?page={n}</c> for the others.</summary>
    /// <param name="context">The test context.</param>
    /// <param name="window">The page window.</param>
    /// <param name="label">The accessible name of the navigation.</param>
    /// <param name="configure">Further parameters, applied last.</param>
    /// <returns>The rendered pager.</returns>
    public static IRenderedComponent<Pager> RenderPager(this BunitContext context, PageWindow window, string label = "Photo pages",
        Action<ComponentParameterCollectionBuilder<Pager>>? configure = null)
        => context.Render<Pager>(parameters =>
        {
            parameters
                .Add(pager => pager.Window, window)
                .Add(pager => pager.PageHref, Href)
                .Add(pager => pager.Label, label)
                .Add(pager => pager.PreviousText, "Previous")
                .Add(pager => pager.NextText, "Next");
            configure?.Invoke(parameters);
        });

    /// <summary>Builds the address of a page: the bare path for page 1, so the first page has one canonical URL.</summary>
    /// <param name="page">The page number.</param>
    /// <returns>The address.</returns>
    public static LinkTarget Href(int page) =>
        LinkTarget.FromString(page == 1 ? "/fotos" : "/fotos?page=" + page.ToString(CultureInfo.InvariantCulture));

    /// <summary>Reads the texts of the numbered links and ellipses, without the previous and next links.</summary>
    /// <param name="cut">The rendered pager.</param>
    /// <returns>The page numbers and ellipses in order.</returns>
    public static string[] NumberedPages(this IRenderedComponent<Pager> cut) =>
        [.. cut.FindAll("li").Select(item => item.TextContent.Trim()).Where(text => text is not "Previous" and not "Next")];
}
