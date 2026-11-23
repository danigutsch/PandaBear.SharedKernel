using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PandaBear.InputNormalization;
using PandaBear.Processes;

namespace PandaBear.DevContainers;

/// <summary>
/// Implements <c>verify-devcontainer</c>: starts the repository's dev container with the Dev Container CLI, runs the
/// given commands in it, and removes it.
/// </summary>
public static partial class DevcontainerVerification
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
    /// <exception cref="TimeoutException">The <c>git</c> that finds the common directory of a linked worktree does not finish within 30 seconds.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/>, <paramref name="host"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    public static async Task<int> Run(DevcontainerVerificationOptions options, DevcontainerHost host, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(output);

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
            await output.WriteLineAsync($"Logs written to '{options.LogDirectory}'.".AsMemory(), CancellationToken.None).ConfigureAwait(false);
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
    private static Task<string?> FindGitCommonDirectory(string root, CancellationToken ct) =>
        FindGitCommonDirectory(root, PathExecutableResolver.Default, GitStartInfo.Timeout, ct);

    /// <summary>
    /// Returns the common Git directory of the linked worktree at <paramref name="root"/>, or <see langword="null"/> when
    /// <paramref name="root"/> is not one, no <c>git</c> is found, or <c>git</c> fails. Git runs as <see cref="GitStartInfo"/>
    /// describes it, through a <see cref="BoundedProcessRunner"/> that stops it after <paramref name="timeout"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Git cannot start or writes more output than the limit.</exception>
    /// <exception cref="TimeoutException">Git does not finish within <paramref name="timeout"/>; it has been stopped.</exception>
    internal static async Task<string?> FindGitCommonDirectory(string root, IExecutableResolver resolver, TimeSpan timeout, CancellationToken ct)
    {
        if (!File.Exists(Path.Combine(root, ".git")) || resolver.Find("git") is null)
        {
            return null;
        }

        var startInfo = GitStartInfo.Create(root, ["-C", root, "rev-parse", "--git-common-dir"], resolver);
        var result = await new BoundedProcessRunner(timeout).Run(startInfo, forward: null, ct).ConfigureAwait(false);
        var commonDirectory = result.StandardOutput.Trim();
        return result.ExitCode == 0 && commonDirectory.Length > 0 ? Path.GetFullPath(commonDirectory, root) : null;
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

    private static Task WriteMetadata(DevcontainerVerificationOptions options, DevcontainerCli cli, string containerId, CancellationToken ct) =>
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
