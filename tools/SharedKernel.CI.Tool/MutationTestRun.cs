using System.Globalization;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>Runs the Stryker.NET configs of <c>.config/sharedkernel-ci.json</c> with a <c>dotnet-stryker</c> installed on demand.</summary>
internal static class MutationTestRun
{
    private const string PackageId = "dotnet-stryker";

    /// <summary>
    /// Installs the configured <c>dotnet-stryker</c> into <c>TestResults/tools/dotnet-stryker/&lt;version&gt;</c> under the
    /// root unless it is there, then runs each config from its directory, in order, and stops at the first failure.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="mutation">The configured Stryker version and config files.</param>
    /// <param name="run">Runs a child process and returns its standard output.</param>
    /// <param name="output">Receives a line for each config and the output of each Stryker run.</param>
    /// <param name="ct">Cancels the running process.</param>
    /// <exception cref="InvalidOperationException">A config file is missing, or the installation or a Stryker run failed.</exception>
    public static async Task Run(
        string root,
        MutationConfiguration mutation,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        TextWriter output,
        CancellationToken ct)
    {
        var configFiles = new string[mutation.ConfigFiles.Length];
        for (var index = 0; index < configFiles.Length; index++)
        {
            configFiles[index] = RepositoryPath.Resolve(root, mutation.ConfigFiles[index]);
            if (!File.Exists(configFiles[index]))
            {
                throw CiConfigurationFile.Invalid(
                    "$.mutation.configFiles[" + index.ToString(CultureInfo.InvariantCulture) + "] names " + mutation.ConfigFiles[index] + ", which does not exist.");
            }
        }

        // A directory per version keeps the tool out of the tool manifest, so other tool restores never download it.
        var toolPath = Path.Combine(root, "TestResults", "tools", PackageId, mutation.StrykerVersion);
        var stryker = Path.Combine(toolPath, OperatingSystem.IsWindows() ? PackageId + ".exe" : PackageId);
        if (!File.Exists(stryker))
        {
            await output.WriteAsync(await run(
                "dotnet", ["tool", "install", PackageId, "--version", mutation.StrykerVersion, "--tool-path", toolPath], root, ct).ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        for (var index = 0; index < configFiles.Length; index++)
        {
            await output.WriteLineAsync("==> " + mutation.ConfigFiles[index]).ConfigureAwait(false);
            await output.WriteAsync(await run(
                stryker, ["--config-file", Path.GetFileName(configFiles[index])], Path.GetDirectoryName(configFiles[index])!, ct).ConfigureAwait(false))
                .ConfigureAwait(false);
        }
    }
}
