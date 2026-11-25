using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SharedKernel.AspNetCore;

/// <summary>Resolves local paths for named ASP.NET Core endpoints.</summary>
public static class NamedEndpointPathExtensions
{
    /// <summary>Resolves the named endpoint with explicit route values and the current request path base.</summary>
    /// <param name="generator">The endpoint link generator.</param>
    /// <param name="context">The current request context.</param>
    /// <param name="endpointName">The registered endpoint name.</param>
    /// <param name="values">The explicit values for the route parameters.</param>
    /// <returns>The generated path, including the request path base.</returns>
    /// <exception cref="ArgumentNullException">The generator, context, or endpoint name is null.</exception>
    /// <exception cref="InvalidOperationException">The endpoint cannot generate a path with the supplied values.</exception>
    public static string GetRequiredPathByName(this LinkGenerator generator, HttpContext context, string endpointName, RouteValueDictionary? values = null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(context);

        return generator.GetPathByName(context, endpointName, values)
            ?? throw new InvalidOperationException($"Cannot generate a path to the named endpoint '{endpointName}'.");
    }
}
