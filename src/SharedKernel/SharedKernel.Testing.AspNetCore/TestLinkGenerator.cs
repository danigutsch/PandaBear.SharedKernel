using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SharedKernel.Testing.AspNetCore;

/// <summary>
/// Provides deterministic named-route paths for ASP.NET Core tests.
/// </summary>
internal sealed class TestLinkGenerator(Func<string, RouteValueDictionary, string?> getPath) : LinkGenerator
{
    private readonly Func<string, RouteValueDictionary, string?> getPath = getPath ?? throw new ArgumentNullException(nameof(getPath));

    /// <inheritdoc />
    public override string? GetPathByAddress<TAddress>(
        HttpContext? httpContext,
        TAddress address,
        RouteValueDictionary values,
        RouteValueDictionary? ambientValues = null,
        PathString? pathBase = null,
        FragmentString fragment = default,
        LinkOptions? options = null)
    {
        return address is string endpointName ? getPath(endpointName, values) : null;
    }

    /// <inheritdoc />
    public override string? GetPathByAddress<TAddress>(
        TAddress address,
        RouteValueDictionary values,
        PathString pathBase = default,
        FragmentString fragment = default,
        LinkOptions? options = null)
    {
        return address is string endpointName ? getPath(endpointName, values) : null;
    }

    /// <inheritdoc />
    public override string? GetUriByAddress<TAddress>(
        HttpContext? httpContext,
        TAddress address,
        RouteValueDictionary values,
        RouteValueDictionary? ambientValues = null,
        string? scheme = null,
        HostString? host = null,
        PathString? pathBase = null,
        FragmentString fragment = default,
        LinkOptions? options = null)
    {
        return address is string endpointName ? getPath(endpointName, values) : null;
    }

    /// <inheritdoc />
    public override string? GetUriByAddress<TAddress>(
        TAddress address,
        RouteValueDictionary values,
        string scheme,
        HostString host,
        PathString pathBase = default,
        FragmentString fragment = default,
        LinkOptions? options = null)
    {
        return address is string endpointName ? getPath(endpointName, values) : null;
    }
}
