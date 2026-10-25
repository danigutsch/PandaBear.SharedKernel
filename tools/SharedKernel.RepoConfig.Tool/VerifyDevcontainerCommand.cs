using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Implements <c>verify-devcontainer</c>: starts the repository's dev container with the Dev Container CLI, runs the
/// given commands in it, and removes it.
/// </summary>
internal static partial class VerifyDevcontainerCommand
{
    /// <summary>The cache directory of the bootstrapped CLI, relative to the root.</summary>
    public const string ToolsDirectory = "TestResults/devcontainer-tools";

    /// <summary>The default log directory, relative to the root.</summary>
    public const string DefaultLogDirectory = "TestResults/devcontainer-smoke";

    /// <summary>
    /// Runs the dev container validation and returns 0 when <c>devcontainer up</c> reports success and every command
    /// exits with 0.
    /// </summary>
    /// <exception cref="InvalidOperationException">A prerequisite is missing, or <c>up</c> or a command fails.</exception>
    public static async Task<int> Run(VerifyDevcontainerOptions options, DevcontainerHost host, TextWriter output, CancellationToken ct)
    {
        var root = options.Root;
        if (!File.Exists(Path.Combine(root, ".devcontainer", "devcontainer.json")))
        {
            throw new InvalidOperationException($"Could not find .devcontainer/devcontainer.json under '{root}'.");
        }

        var docker = host.DockerExecutable
            ?? throw new InvalidOperationException("docker was not found in an absolute PATH directory.");
        var cli = options.DevcontainerCli is { } path ? new DevcontainerCli(path, null) : await Bootstrap(root, host, ct).ConfigureAwait(false);
        List<string> upArguments = ["up", "--workspace-folder", root, "--log-level", "trace"];
        if (await FindGitCommonDirectory(root, ct).ConfigureAwait(false) is { } commonDirectory)
        {
            upArguments.AddRange(["--mount", $"type=bind,source={commonDirectory},target={commonDirectory}"]);
        }

        Directory.CreateDirectory(options.LogDirectory);
        string? containerId = null;
        try
        {
            await WriteStep(output, "Building and starting the devcontainer").ConfigureAwait(false);
            var upLog = Path.Combine(options.LogDirectory, "devcontainer-up.log");
            StringBuilder upResult = new();
            var upExitCode = await RunCli(cli, root, upArguments, output, upLog, upResult, ct).ConfigureAwait(false);
            var upOutput = await File.ReadAllTextAsync(upLog, ct).ConfigureAwait(false);

            // The two streams interleave in the log in arrival order, so the result on standard output wins over trace
            // lines on standard error.
            containerId = LastContainerId(upResult.ToString()) ?? LastContainerId(upOutput);
            if (containerId is not null)
            {
                await WriteMetadata(options, cli, containerId, ct).ConfigureAwait(false);
            }

            // The CLI can report a failed outcome and still exit with 0.
            if (upExitCode != 0 || upOutput.Contains("\"outcome\":\"error\"", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"devcontainer up failed. Inspect '{upLog}' for details.");
            }

            if (containerId is null)
            {
                throw new InvalidOperationException($"Failed to determine the devcontainer container ID from '{upLog}'.");
            }

            for (var index = 1; index <= options.Commands.Count; index++)
            {
                var number = index.ToString(CultureInfo.InvariantCulture);
                await WriteStep(output, $"Running command {number} inside the devcontainer").ConfigureAwait(false);
                var execLog = Path.Combine(options.LogDirectory, $"devcontainer-exec-{number}.log");
                var exitCode = await RunCli(
                    cli,
                    root,
                    ["exec", "--workspace-folder", root, "bash", "-lc", options.Commands[index - 1]],
                    output,
                    execLog,
                    null,
                    ct).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"devcontainer exec {number} failed with exit code {exitCode.ToString(CultureInfo.InvariantCulture)}. Inspect '{execLog}' for details.");
                }
            }

            await WriteStep(output, "Devcontainer validation completed successfully").ConfigureAwait(false);
            await output.WriteLineAsync($"Logs written to '{options.LogDirectory}'.").ConfigureAwait(false);
            return 0;
        }
        finally
        {
            if (containerId is not null)
            {
                await RemoveContainer(docker, containerId, options.KeepContainer, output).ConfigureAwait(false);
            }
        }
    }

    private static async Task<DevcontainerCli> Bootstrap(string root, DevcontainerHost host, CancellationToken ct)
    {
        if (!host.IsLinuxX64)
        {
            throw new InvalidOperationException(
                "The pinned Dev Container CLI is bootstrapped only on Linux x64 hosts; pass --devcontainer-cli <path> on this host.");
        }

        var toolsDirectory = Path.Combine(root, ToolsDirectory);
        if (host.HttpClient is { } httpClient)
        {
            return await DevcontainerCliBootstrap.Ensure(toolsDirectory, host.Pins, httpClient, ct).ConfigureAwait(false);
        }

        using HttpClient defaultClient = new()
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        return await DevcontainerCliBootstrap.Ensure(toolsDirectory, host.Pins, defaultClient, ct).ConfigureAwait(false);
    }

    // A linked worktree's .git file points into the main repository, which the container needs for Git to work.
    private static async Task<string?> FindGitCommonDirectory(string root, CancellationToken ct)
    {
        var git = ExecutableResolver.Resolve("git", Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows());
        if (!File.Exists(Path.Combine(root, ".git")) || git is null)
        {
            return null;
        }

        ProcessStartInfo startInfo = new(git)
        {
            WorkingDirectory = root
        };
        foreach (var argument in new[] { "-C", root, "rev-parse", "--git-common-dir" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using StringWriter standardOutput = new(CultureInfo.InvariantCulture);
        var exitCode = await DevcontainerProcess.Run(startInfo, standardOutput, TextWriter.Null, null, null, ct).ConfigureAwait(false);
        var commonDirectory = standardOutput.ToString().Trim();
        return exitCode == 0 && commonDirectory.Length > 0 ? Path.GetFullPath(commonDirectory, root) : null;
    }

    private static Task<int> RunCli(
        DevcontainerCli cli,
        string root,
        IEnumerable<string> arguments,
        TextWriter output,
        string logPath,
        StringBuilder? capturedOutput,
        CancellationToken ct)
    {
        ProcessStartInfo startInfo = new(cli.Executable)
        {
            WorkingDirectory = root
        };
        if (cli.Script is not null)
        {
            startInfo.ArgumentList.Add(cli.Script);
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return DevcontainerProcess.Run(startInfo, output, output, logPath, capturedOutput, ct);
    }

    private static Task WriteMetadata(VerifyDevcontainerOptions options, DevcontainerCli cli, string containerId, CancellationToken ct) =>
        File.WriteAllTextAsync(
            Path.Combine(options.LogDirectory, "metadata.txt"),
            $"""
            workspace_folder={options.Root}
            log_dir={options.LogDirectory}
            devcontainer_cli={cli.Script ?? cli.Executable}
            container_id={containerId}

            """.ReplaceLineEndings("\n"),
            ct);

    // Cleanup also runs after cancellation, so it has its own deadline. A failed removal is reported, not raised.
    private static async Task RemoveContainer(string docker, string containerId, bool keepContainer, TextWriter output)
    {
        var escapedId = ControlCharacters.Escape(containerId);
        if (keepContainer)
        {
            await output.WriteLineAsync($"Keeping devcontainer '{escapedId}' because --keep-container is set.").ConfigureAwait(false);
            return;
        }

        await WriteStep(output, $"Cleaning up devcontainer {escapedId}").ConfigureAwait(false);
        ProcessStartInfo startInfo = new(docker);
        foreach (var argument in new[] { "rm", "-f", containerId })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using CancellationTokenSource deadline = new(TimeSpan.FromMinutes(1));
        try
        {
            if (await DevcontainerProcess.Run(startInfo, TextWriter.Null, TextWriter.Null, null, null, deadline.Token).ConfigureAwait(false) == 0)
            {
                return;
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or InvalidOperationException or IOException)
        {
            // Reported below.
        }

        await output.WriteLineAsync($"Could not remove devcontainer '{escapedId}'; remove it with docker rm -f.").ConfigureAwait(false);
    }

    private static Task WriteStep(TextWriter output, string message) =>
        output.WriteLineAsync($"{Environment.NewLine}==> {message}");

    private static string? LastContainerId(string text)
    {
        var matches = ContainerIdPattern().Matches(text);
        return matches.Count > 0 ? matches[^1].Groups["id"].Value : null;
    }

    [GeneratedRegex("\"containerId\":\"(?<id>[^\"]+)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 10_000)]
    private static partial Regex ContainerIdPattern();
}
