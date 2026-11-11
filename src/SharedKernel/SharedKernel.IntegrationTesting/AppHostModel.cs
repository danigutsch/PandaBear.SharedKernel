using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace PandaBear.IntegrationTesting;

/// <summary>
/// Reads an AppHost's application model without starting it, for fast tests of the wiring: which project waits for what, which variables
/// and references it receives, and whether it has a health check. Build the model with
/// <c>DistributedApplicationTestingBuilder.CreateAsync&lt;TAppHost&gt;</c> or <see cref="AspireTestApplication.CreateBuilder(string[])"/>.
/// </summary>
public static class AppHostModel
{
    /// <summary>Finds the project resource with the given name.</summary>
    /// <param name="builder">The AppHost builder.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>The project.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <exception cref="InvalidOperationException">The model has no project with that name.</exception>
    public static ProjectResource Project(IDistributedApplicationTestingBuilder builder, string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return builder.Resources.OfType<ProjectResource>().SingleOrDefault(resource => string.Equals(resource.Name, name, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"The application model has no project named '{name}'. Projects: {string.Join(", ", builder.Resources.OfType<ProjectResource>().Select(project => project.Name))}.");
    }

    /// <summary>Gets the names of the resources a resource waits for (<c>WaitFor</c> and <c>WaitForCompletion</c>).</summary>
    /// <param name="resource">The resource.</param>
    /// <returns>The names, in declaration order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resource"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> WaitedFor(IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return [.. resource.Annotations.OfType<WaitAnnotation>().Select(annotation => annotation.Resource.Name)];
    }

    /// <summary>
    /// Runs a resource's environment callbacks as a publish would, so values stay model objects (a <see cref="ConnectionStringReference"/>, an
    /// <see cref="EndpointReference"/>, a parameter, a plain string) instead of addresses that exist only once the application runs.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The environment variables by name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resource"/> is <see langword="null"/>.</exception>
    public static async Task<IReadOnlyDictionary<string, object>> EvaluateEnvironment(IResource resource, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var variables = new Dictionary<string, object>(StringComparer.Ordinal);
        var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish), resource, variables, ct);
        foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await callback.Callback(context).ConfigureAwait(false);
        }

        return variables;
    }

    /// <summary>Gets whether a resource has an HTTP health check (<c>WithHttpHealthCheck</c>), optionally for one path.</summary>
    /// <param name="resource">The resource.</param>
    /// <param name="path">The health path, such as <c>/health</c>; any path when <see langword="null"/>.</param>
    /// <returns>Whether the resource has such a check.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resource"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Aspire names an HTTP health check <c>{resource}_{endpoint}_{path}_{status}_check</c>; the check is recognized by that key, which is the
    /// only trace it leaves on the resource.
    /// </remarks>
    public static bool HasHttpHealthCheck(IResource resource, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var prefix = resource.Name + "_";
        return resource.Annotations.OfType<HealthCheckAnnotation>().Any(annotation =>
            annotation.Key.StartsWith(prefix, StringComparison.Ordinal)
            && annotation.Key.EndsWith("_check", StringComparison.Ordinal)
            && (path is null
                ? annotation.Key.Contains("_/", StringComparison.Ordinal)
                : annotation.Key.Contains("_" + path + "_", StringComparison.Ordinal)));
    }

    /// <summary>Gets the resource that a connection-string value of <see cref="EvaluateEnvironment"/> refers to.</summary>
    /// <param name="value">The variable value.</param>
    /// <returns>The resource.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The value is not a connection-string reference.</exception>
    public static IResource ConnectionResource(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value switch
        {
            ConnectionStringReference reference => reference.Resource,
            IResourceWithConnectionString resource => resource,
            _ => throw new InvalidOperationException($"Not a connection-string reference: {value.GetType().Name}."),
        };
    }
}
