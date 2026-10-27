using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging.Abstractions;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Resolves the environment variables a resource would receive when it runs.</summary>
internal static class EnvironmentVariables
{
    /// <summary>Evaluates the resource's environment-variable callbacks in a run-mode context, with a 10-second timeout, and returns the processed values.</summary>
    /// <param name="resource">The resource to evaluate.</param>
    /// <returns>The environment variable names mapped to their processed values.</returns>
    public static async Task<IReadOnlyDictionary<string, string>> Of(IResource resource)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var configuration = await ExecutionConfigurationBuilder
            .Create(resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(
                new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
                NullLogger.Instance,
                timeout.Token);

        return configuration.EnvironmentVariablesWithUnprocessed.ToDictionary(variable => variable.Key, variable => variable.Value.Processed);
    }
}
