using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SharedKernel.Blazor.Site;

/// <summary>The HTTP methods of the pages of a site without forms.</summary>
public static class RazorComponentMethodExtensions
{
    private static readonly string[] GetAndHead = [HttpMethods.Get, HttpMethods.Head];

    /// <summary>
    /// Lets the routed components answer GET and HEAD only (HEAD as GET; the server leaves the body out), so any other method gets 405 with
    /// <c>Allow: GET, HEAD</c>. The components in <paramref name="anyMethodComponents" />, such as the error page, answer every method,
    /// because the error handlers re-execute a failed request with its own method and routing matches it before the page can change it.
    /// </summary>
    /// <param name="builder">The Razor component endpoints.</param>
    /// <param name="anyMethodComponents">The routed components that answer every method.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> or <paramref name="anyMethodComponents" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="anyMethodComponents" /> holds <see langword="null" />.</exception>
    public static RazorComponentsEndpointConventionBuilder AllowOnlyGetAndHead(
        this RazorComponentsEndpointConventionBuilder builder,
        params Type[] anyMethodComponents)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(anyMethodComponents);
        if (anyMethodComponents.Any(static component => component is null))
        {
            throw new ArgumentException("Name each component that answers every method.", nameof(anyMethodComponents));
        }

        HashSet<Type> anyMethod = [.. anyMethodComponents];
        builder.Add(endpoint => Allow(endpoint, endpoint.Metadata.OfType<ComponentTypeMetadata>().Any(page => anyMethod.Contains(page.Type)) ? [] : GetAndHead));
        return builder;
    }

    private static void Allow(EndpointBuilder endpoint, string[] methods)
    {
        foreach (var mapped in endpoint.Metadata.OfType<IHttpMethodMetadata>().ToList())
        {
            endpoint.Metadata.Remove(mapped);
        }

        if (methods.Length > 0)
        {
            endpoint.Metadata.Add(new HttpMethodMetadata(methods));
        }
    }
}
