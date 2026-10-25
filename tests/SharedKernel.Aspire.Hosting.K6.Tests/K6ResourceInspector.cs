using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging.Abstractions;

namespace PandaBear.Aspire.Hosting.K6.Tests;

internal static class K6ResourceInspector
{
    internal static async Task<(string[] Arguments, Dictionary<string, string> Environment)> Evaluate(IResource resource)
    {
        var configuration = await ExecutionConfigurationBuilder.Create(resource)
            .WithArgumentsConfig()
            .WithEnvironmentVariablesConfig()
            .BuildAsync(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run), NullLogger.Instance);

        return (
            [.. configuration.Arguments.Select(static argument => argument.Value)],
            configuration.EnvironmentVariables.ToDictionary(static pair => pair.Key, static pair => pair.Value));
    }

    /// <summary>Collects the environment values without resolving endpoints, which need a running orchestrator.</summary>
    internal static async Task<Dictionary<string, object>> UnresolvedEnvironment(IResource resource)
    {
        var variables = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run), resource, variables);
        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        return variables;
    }
}
