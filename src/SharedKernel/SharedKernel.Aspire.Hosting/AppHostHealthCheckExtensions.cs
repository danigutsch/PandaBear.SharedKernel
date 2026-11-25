using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;
using SharedKernel.AspNetCore;

namespace Aspire.Hosting;

/// <summary>Runs a project in the AppHost environment with a health check that matches the endpoint the project maps.</summary>
public static class AppHostHealthCheckExtensions
{
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
}
