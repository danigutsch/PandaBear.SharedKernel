namespace PandaBear.Versioning;

/// <summary>Packs SharedKernel projects from the working tree into a feed for a consumer that develops against uncommitted changes.</summary>
internal static class DevPack
{
    /// <summary>Packs the planned projects, evicts them from the NuGet cache and reports each step.</summary>
    /// <param name="commands">Runs <c>dotnet</c>.</param>
    /// <param name="options">The pack options.</param>
    /// <param name="output">Receives one line per project.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The feed directory.</returns>
    public static async Task<string> Run(ExternalCommands commands, DevPackOptions options, TextWriter output, CancellationToken ct)
    {
        var resolved = options with { RepoRoot = Path.GetFullPath(options.RepoRoot) };
        var feed = Path.GetFullPath(options.Feed);
        Directory.CreateDirectory(feed);

        var consumerDirectory = options.ConsumerDirectory;
        var cache = await SharedKernelDevPackPlanner.ResolveGlobalPackagesDirectory(
            consumerDirectory,
            explicitDirectory: null,
            options.NuGetPackages,
            async token => SharedKernelDevPackPlanner.ParseGlobalPackagesListing(
                await commands.RunOrDefault("dotnet", ["nuget", "locals", "global-packages", "--list"], consumerDirectory, token).ConfigureAwait(false)),
            ct).ConfigureAwait(false);

        foreach (var invocation in CreateInvocations(resolved, feed))
        {
            if (!invocation.Step.Rebuild)
            {
                await output.WriteLineAsync(FormatKept(invocation.Step)).ConfigureAwait(false);
                continue;
            }

            await commands.Run("dotnet", invocation.Arguments, invocation.WorkingDirectory, ct).ConfigureAwait(false);
            var evicted = SharedKernelDevPackPlanner.EvictFromGlobalPackages(invocation.Step.PackageId, resolved.Version, cache);
            await output.WriteLineAsync(
                "Packed " + invocation.Step.PackageId + " " + options.Version + (evicted ? " (evicted from " + cache + ")" : " (no cached copy in " + cache + ")")).ConfigureAwait(false);
        }

        await output.WriteLineAsync("SharedKernel dev packages: " + feed).ConfigureAwait(false);
        return feed;
    }

    /// <summary>Formats the report line of a kept step, with a warning when its sources changed after it was packed.</summary>
    /// <param name="step">The kept step.</param>
    /// <returns>The line.</returns>
    internal static string FormatKept(DevPackStep step) =>
        step.Stale
            ? "Kept   " + step.PackageId + " (stale: sources changed after it was packed; name it to repack)"
            : "Kept   " + step.PackageId;

    /// <summary>Plans a <c>dotnet pack</c> call for every step (kept steps included, see <see cref="DevPackStep.Rebuild"/>), without running anything.</summary>
    /// <param name="options">The pack options.</param>
    /// <param name="feed">The absolute feed directory.</param>
    /// <returns>The invocations in dependency order.</returns>
    internal static IReadOnlyList<DevPackInvocation> CreateInvocations(DevPackOptions options, string feed)
    {
        var repoRoot = Path.GetFullPath(options.RepoRoot);
        var steps = SharedKernelDevPackPlanner.Plan(repoRoot, options.Projects, feed, options.Version);
        var packageOptions = new SharedKernelPackageOptions(options.Version, feed, repoRoot);
        return [.. steps
            .Select(step => new DevPackInvocation(
                step,
                SharedKernelPackagePlanner.CreatePackArguments(step.ProjectPath, packageOptions, feed),
                repoRoot))];
    }
}
