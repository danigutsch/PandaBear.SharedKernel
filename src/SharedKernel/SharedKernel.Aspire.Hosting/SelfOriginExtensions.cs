using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>Gives a resource the URL of one of its own endpoints.</summary>
public static class SelfOriginExtensions
{
    /// <summary>
    /// Gives a resource the URL of one of its own endpoints in an environment variable, such as the canonical origin that a public site
    /// builds absolute links from: <c>site.WithSelfOrigin("PublicSite__Origin")</c>.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="builder">The resource.</param>
    /// <param name="environmentVariable">The variable that receives the URL, such as <c>https://localhost:7043</c>.</param>
    /// <param name="endpointName">
    /// The endpoint. When <see langword="null"/>, the <c>https</c> endpoint, or the <c>http</c> endpoint when there is no <c>https</c> one.
    /// A name that is given must exist; there is no fallback for it.
    /// </param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="environmentVariable"/> is blank, or <paramref name="endpointName"/> is empty or white space.</exception>
    /// <remarks>
    /// <para>
    /// Follows the Aspire rule never to hard-code a URL in <c>WithEnvironment</c>: the value is an endpoint reference, so it is the
    /// allocated address when the application runs and an endpoint expression in a publish manifest.
    /// </para>
    /// <para>
    /// The endpoint is looked up when the environment is built, so endpoints added after this call (a launch profile, a later
    /// <c>WithHttpsEndpoint</c>) count. Without a name, falling back from <c>https</c> to <c>http</c> lets the same AppHost run where a launch
    /// profile or a test provides only HTTP; an explicit name means that endpoint and nothing else, and a missing one fails the resource
    /// start with an <see cref="InvalidOperationException"/> that names it, instead of silently using another address.
    /// </para>
    /// <para>
    /// A project or executable receives its address on the host (<c>localhost</c>). A container receives its address in the container
    /// network, which a browser cannot reach, so do not use this for the public origin of a container.
    /// </para>
    /// </remarks>
    public static IResourceBuilder<T> WithSelfOrigin<T>(this IResourceBuilder<T> builder, string environmentVariable, string? endpointName = null)
        where T : IResourceWithEndpoints, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentVariable);
        if (endpointName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        }

        var resource = builder.Resource;
        return builder.WithEnvironment(context => context.EnvironmentVariables[environmentVariable] = SelfEndpoint(resource, environmentVariable, endpointName));
    }

    private static EndpointReference SelfEndpoint(IResourceWithEndpoints resource, string environmentVariable, string? endpointName)
    {
        var names = resource.Annotations.OfType<EndpointAnnotation>().Select(endpoint => endpoint.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (endpointName is not null)
        {
            return names.Contains(endpointName)
                ? resource.GetEndpoint(endpointName)
                : throw new InvalidOperationException($"Resource '{resource.Name}' has no endpoint named '{endpointName}' for {environmentVariable}.");
        }

        var fallback = names.Contains("https") ? "https" : names.Contains("http") ? "http" : null;
        return fallback is not null
            ? resource.GetEndpoint(fallback)
            : throw new InvalidOperationException($"Resource '{resource.Name}' has neither an 'https' nor an 'http' endpoint for {environmentVariable}.");
    }
}
