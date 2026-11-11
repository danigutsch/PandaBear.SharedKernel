using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;
using PandaBear.AspNetCore;

namespace Aspire.Hosting;

/// <summary>Resource builder helpers that are not tied to one container.</summary>
public static class SharedKernelResourceBuilderExtensions
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

    /// <summary>
    /// Runs a .NET project in the AppHost's environment (<c>ASPNETCORE_ENVIRONMENT</c>, not its launch profile's) and adds
    /// <c>WithHttpHealthCheck(path)</c> exactly when the project maps that endpoint, so the check and the endpoint cannot disagree.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="builder">The resource.</param>
    /// <param name="path">The health path the project maps, <c>/health</c> by default.</param>
    /// <param name="onlyInDevelopment">
    /// Whether the project maps the endpoint only in Development (<c>HealthEndpointExposure.DevelopmentOnly</c> of SharedKernel.ServiceDefaults).
    /// When <see langword="true"/>, the check is added only when the AppHost environment is Development.
    /// </param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is blank or does not start with <c>/</c>.</exception>
    /// <remarks>
    /// Follows the Aspire rule that a resource another resource <c>WaitFor</c>s needs <c>WithHttpHealthCheck("/health")</c>, which the
    /// service defaults' <c>MapDefaultEndpoints</c> serves. A check against an endpoint the project does not map never turns healthy, and
    /// everything that waits for the project then waits forever; a missing check makes <c>WaitFor</c> stop waiting at "running".
    /// </remarks>
    public static IResourceBuilder<T> WithHttpHealthCheckInAppHostEnvironment<T>(this IResourceBuilder<T> builder, string path = "/health", bool onlyInDevelopment = false)
        where T : IResourceWithEndpoints, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path[0] != '/')
        {
            throw new ArgumentException("The health path must start with '/'.", nameof(path));
        }

        var environment = builder.ApplicationBuilder.Environment;
        builder.WithEnvironment(HostEnvironmentVariableNames.AspNetCoreEnvironment, environment.EnvironmentName);
        if (!onlyInDevelopment || environment.IsDevelopment())
        {
            builder.WithHttpHealthCheck(path);
        }

        return builder;
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
