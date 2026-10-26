using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging.Abstractions;

namespace PandaBear.Aspire.Hosting.Tests;

internal static class EnvironmentVariables
{
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
