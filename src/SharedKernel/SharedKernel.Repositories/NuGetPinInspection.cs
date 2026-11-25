using SharedKernel.Processes;

namespace SharedKernel.Repositories;

/// <summary>Inspects the evaluated ownership and requesting paths of one restored NuGet package.</summary>
public static class NuGetPinInspection
{
    /// <summary>Reports the selected package across the solution, including lock-exempt projects.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="packageId">The NuGet package ID.</param>
    /// <param name="output">The report destination.</param>
    /// <param name="error">The destination for incomplete evidence diagnostics.</param>
    /// <param name="cancellationToken">Cancels the SDK evaluation and report.</param>
    /// <returns>Zero for a complete report or one when required evidence is unavailable.</returns>
    /// <exception cref="ArgumentNullException">An output writer is null.</exception>
    public static async Task<int> Run(
        string root, string packageId, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (string.IsNullOrWhiteSpace(packageId) || packageId.Length > 100
            || packageId[0] == '-'
            || !packageId.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_'))
        {
            await error.WriteLineAsync("Specify a valid NuGet package ID.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }

        try
        {
            root = Path.GetFullPath(root);
            var source = await NuGetRestoreGraphComparison.Read(root, receipt: null, cancellationToken).ConfigureAwait(false);
            var unavailable = source.Projects.Where(pair => pair.Value.Unavailable is not null).ToArray();
            if (unavailable.Length != 0)
            {
                foreach (var (project, evidence) in unavailable)
                {
                    await error.WriteLineAsync($"{project}: {evidence.Unavailable}".AsMemory(), cancellationToken).ConfigureAwait(false);
                }

                return 1;
            }

            var selected = source.Projects
                .SelectMany(project => (project.Value.Graph?.FindPackage(packageId) ?? [])
                    .Select(match => (Project: project.Key, project.Value.LockExempt, match.Target, match.Package)))
                .ToArray();
            List<string> fileOwners = [];
            foreach (var file in await NuGetFileApps.Find(root, cancellationToken).ConfigureAwait(false))
            {
                var app = await NuGetFileApps.Evaluate(root, file, cancellationToken).ConfigureAwait(false);
                if (app.Packages.TryGetValue(packageId, out var package))
                {
                    var ownership = package.CentralFile is { } central
                        ? $"direct file-based declaration from {central}"
                        : "direct file-based declaration";
                    fileOwners.Add($"{app.Path} [{app.Framework}]: {package.Version ?? "version unavailable"} ({ownership})");
                }
            }

            await output.WriteLineAsync(
                $"{packageId} at {source.Revision} (SDK {source.SdkVersion}, dirty: {source.Dirty})."
                    .AsMemory(), cancellationToken).ConfigureAwait(false);
            foreach (var owner in fileOwners)
            {
                await output.WriteLineAsync(owner.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            if (selected.Length == 0)
            {
                if (fileOwners.Count == 0)
                {
                    await output.WriteLineAsync("No restored target selects this package.".AsMemory(), cancellationToken).ConfigureAwait(false);
                }

                return 0;
            }

            var dependencyPaths = await NuGetRestoreGraphComparison.RunDotnet(root, new BoundedProcessRunner(),
                ["nuget", "why", Path.Combine(root, source.Solution), packageId], cancellationToken).ConfigureAwait(false);
            if (dependencyPaths.ExitCode != 0 || string.IsNullOrWhiteSpace(dependencyPaths.StandardOutput))
            {
                await error.WriteLineAsync("The SDK could not provide dependency paths.".AsMemory(), cancellationToken).ConfigureAwait(false);
                return 1;
            }

            foreach (var (project, lockExempt, target, package) in selected)
            {
                var lockState = lockExempt ? "lock-exempt" : "locked";
                var result = $"{project} [{target}]: {package.Version} ({package.Classification}, {lockState})";
                await output.WriteLineAsync(result.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            await output.WriteLineAsync(dependencyPaths.StandardOutput.TrimEnd().AsMemory(), cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException
            or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or TimeoutException)
        {
            await error.WriteLineAsync("Could not inspect the restored package graph.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }
    }
}
