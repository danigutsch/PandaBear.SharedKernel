using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// The base of a static server-rendered page under <c>/{culture}/...</c>: it checks the culture segment against
/// <see cref="Cultures" />, reads the content of that language once per render through <see cref="ReadContent" />, lets the page pick
/// what it shows in <see cref="Load" />, and calls <see cref="NavigationManager.NotFound" /> when the culture is not supported or the
/// page has nothing to show, so the response is 404 and the router's not-found page renders.
/// </summary>
/// <remarks>
/// The route template names the culture segment <c>{Culture}</c>, such as <c>@page "/{Culture}/contact"</c>. The page renders its
/// markup only when <see cref="Found" /> is <see langword="true" />. A fixed section whose segment is translated, such as
/// <c>/pt/viagens</c> and <c>/en/tours</c>, routes every language's segment and returns <see cref="InSection" /> from
/// <see cref="Load" />, so each address shows only in its own language.
/// </remarks>
/// <typeparam name="TContent">The content of one language, which the application reads.</typeparam>
public abstract class CulturePageBase<TContent> : ComponentBase
    where TContent : class
{
    private ContentCulture? _language;

    /// <summary>Gets or sets the culture segment of the route.</summary>
    [Parameter]
    public string Culture { get; set; } = string.Empty;

    /// <summary>Gets the language of the page: the culture of the route, or the default culture until a supported one is read.</summary>
    protected ContentCulture Language => _language ?? Cultures.Default;

    /// <summary>Gets the content of <see cref="Language" />, or <see langword="null" /> when the culture is not supported.</summary>
    protected TContent? Content
    {
        get; private set;
    }

    /// <summary>Gets a value indicating whether the page has something to show; when not, it renders nothing and answers 404.</summary>
    protected bool Found
    {
        get; private set;
    }

    /// <summary>
    /// Gets the path of the current request, root-relative, without query, fragment or trailing slash, such as <c>/pt/contato</c>.
    /// </summary>
    protected string CurrentPath => "/" + Navigation.ToBaseRelativePath(Navigation.Uri).Split('?', '#')[0].TrimEnd('/');

    /// <summary>Gets the current request, such as for its query; its aborted token cancels <see cref="ReadContent" />.</summary>
    [CascadingParameter]
    protected HttpContext? HttpContext
    {
        get; private set;
    }

    /// <summary>Gets the cultures of the site; the culture segment must name one of them.</summary>
    protected abstract SupportedCultures Cultures
    {
        get;
    }

    /// <summary>Gets or sets the navigation manager, which reads the current address and answers not found.</summary>
    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>Reads the culture segment and the content of its language, then calls not found when there is nothing to show.</summary>
    /// <returns>The task of the read.</returns>
    protected override async Task OnParametersSetAsync()
    {
        Found = false;
        Content = null;
        _language = null;
        if (Cultures.TryGet(Culture, out var culture))
        {
            _language = culture;
            Content = await ReadContent(culture, HttpContext?.RequestAborted ?? CancellationToken.None).ConfigureAwait(true);
            Found = Load(Content);
        }

        if (!Found)
        {
            Navigation.NotFound();
        }
    }

    /// <summary>
    /// Whether the current path is in a fixed section as spelled in the page's language: the section itself or any path below it,
    /// ignoring case.
    /// </summary>
    /// <param name="section">Builds the root-relative section path of a language, such as <c>/pt/viagens</c> for Portuguese.</param>
    /// <returns><see langword="true" /> when the path is the section of <see cref="Language" /> or below it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="section" /> is <see langword="null" />.</exception>
    protected bool InSection(Func<ContentCulture, string> section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var path = CurrentPath;
        var root = section(Language);
        return path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reads the content of a language.</summary>
    /// <param name="culture">The supported culture of the route.</param>
    /// <param name="cancellationToken">The request's aborted token.</param>
    /// <returns>The content.</returns>
    protected abstract Task<TContent> ReadContent(ContentCulture culture, CancellationToken cancellationToken);

    /// <summary>Picks what the page shows from the content of its language.</summary>
    /// <param name="content">The content of <see cref="Language" />.</param>
    /// <returns><see langword="false" /> when the requested item does not exist, which answers 404.</returns>
    protected abstract bool Load(TContent content);
}
