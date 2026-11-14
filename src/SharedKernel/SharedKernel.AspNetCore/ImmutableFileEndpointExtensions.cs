using System.Collections.Frozen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace PandaBear.AspNetCore;

/// <summary>
/// Serves files whose content never changes under their name, such as uploads stored as <c>&lt;content hash&gt;.&lt;extension&gt;</c>, from one
/// folder, with a year-long immutable cache header.
/// </summary>
public static class ImmutableFileEndpointExtensions
{
    /// <summary>The <c>Cache-Control</c> value of a served file: public, one year, immutable.</summary>
    public const string CacheControl = "public, max-age=31536000, immutable";

    private const string FileNameRouteValue = "fileName";
    private const int MaxFileNameLength = 255;

    /// <summary>
    /// Maps <c>GET</c> and <c>HEAD</c> <c>{routePrefix}/{fileName}</c>. A request is answered with the file only when every check passes, and
    /// with 404 otherwise: the name is a plain file name (ASCII letters, digits, <c>.</c>, <c>-</c> and <c>_</c>, not starting with a dot, at
    /// most 255 characters), its extension is a key of <paramref name="contentTypes"/> (compared ordinally), <paramref name="isServed"/>
    /// approves it, and the file exists directly in <paramref name="folder"/>. The answer carries the mapped media type,
    /// <see cref="CacheControl"/>, <c>X-Content-Type-Options: nosniff</c>, <c>Last-Modified</c>, and supports range requests.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="routePrefix">The path the file names follow, such as <c>/media</c>.</param>
    /// <param name="folder">The folder that holds the files; a relative path is resolved against the current directory.</param>
    /// <param name="contentTypes">The served extensions, with their dot, and their media types, such as <c>.jpg</c> to <c>image/jpeg</c>.</param>
    /// <param name="isServed">
    /// Decides whether a well-formed name may be served, for example because published content uses it. It runs only after the name and
    /// extension checks; use the context's request services and <see cref="HttpContext.RequestAborted"/> for data access.
    /// </param>
    /// <returns>The endpoint convention builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/>, <paramref name="contentTypes"/> or <paramref name="isServed"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="routePrefix"/> does not start with <c>/</c>, <paramref name="folder"/> is blank, or <paramref name="contentTypes"/> is
    /// empty or has a key that is not a dot and a name or a blank media type.
    /// </exception>
    public static IEndpointConventionBuilder MapImmutableFiles(
        this IEndpointRouteBuilder endpoints,
        string routePrefix,
        string folder,
        IReadOnlyDictionary<string, string> contentTypes,
        Func<HttpContext, string, ValueTask<bool>> isServed)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(routePrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(contentTypes);
        ArgumentNullException.ThrowIfNull(isServed);
        if (!routePrefix.StartsWith('/'))
        {
            throw new ArgumentException("The route prefix must start with '/'.", nameof(routePrefix));
        }

        if (contentTypes.Count == 0 || contentTypes.Any(pair => pair.Key.Length < 2 || pair.Key[0] != '.' || !IsPlainFileName(pair.Key[1..]) || string.IsNullOrWhiteSpace(pair.Value)))
        {
            throw new ArgumentException("Map at least one extension, such as '.jpg', to a media type.", nameof(contentTypes));
        }

        var types = contentTypes.ToFrozenDictionary(StringComparer.Ordinal);
        var root = Path.GetFullPath(folder);
        var pattern = $"{routePrefix.TrimEnd('/')}/{{{FileNameRouteValue}}}";
        return endpoints.MapMethods(pattern, [HttpMethods.Get, HttpMethods.Head], context => Serve(context, root, types, isServed));
    }

    private static async Task Serve(
        HttpContext context,
        string root,
        FrozenDictionary<string, string> types,
        Func<HttpContext, string, ValueTask<bool>> isServed)
    {
        if (context.Request.RouteValues[FileNameRouteValue] is not string fileName
            || !IsPlainFileName(fileName)
            || !types.TryGetValue(Path.GetExtension(fileName), out var contentType)
            || !Directory.Exists(root)
            || !await isServed(context, fileName).ConfigureAwait(false))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // The provider resolves only paths inside its root and refuses hidden and system files, in addition to the name check above. It is
        // never watched, so creating one per request costs no file system watcher.
        using var files = new PhysicalFileProvider(root);
        var file = files.GetFileInfo(fileName);
        if (!file.Exists || file.IsDirectory || file.PhysicalPath is not { } path)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.Headers[HeaderNames.CacheControl] = CacheControl;
        context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        var result = TypedResults.PhysicalFile(path, contentType, lastModified: file.LastModified, enableRangeProcessing: true);
        await result.ExecuteAsync(context).ConfigureAwait(false);
    }

    private static bool IsPlainFileName(string name)
    {
        if (name.Length is 0 or > MaxFileNameLength || name[0] == '.')
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
