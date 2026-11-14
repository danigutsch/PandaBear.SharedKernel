using System.Diagnostics;
using System.Globalization;

namespace PandaBear.CI.Tool;

/// <summary>Installs Playwright browsers with the <c>playwright.ps1</c> that a built project's output holds.</summary>
internal static class PlaywrightInstallation
{
    /// <summary>How long the whole installation may run before it is stopped.</summary>
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)];

    /// <summary>Finds <c>playwright.ps1</c> through the evaluated <c>OutputPath</c> of a project and runs its install command.</summary>
    /// <param name="root">The repository root, the working directory of both processes.</param>
    /// <param name="project">The project, relative to the root.</param>
    /// <param name="browsers">The browsers; none installs Playwright's default browsers.</param>
    /// <param name="withDependencies">Whether to also install the browsers' operating system dependencies.</param>
    /// <param name="configuration">The configuration whose output holds the installer, or <see langword="null"/> for the project default.</param>
    /// <param name="run">Runs a child process and returns its standard output.</param>
    /// <param name="log">Receives a line for each failed install attempt that is retried.</param>
    /// <param name="ct">Cancels the processes.</param>
    /// <param name="timeout">How long both processes together may run, or <see langword="null"/> for <see cref="DefaultTimeout"/>.</param>
    /// <param name="onlyShell">Whether to install only the headless shell of Chromium, not the full browser.</param>
    /// <param name="timeProvider">Times the waits between install attempts, or <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    /// <returns>The installer's standard output, preceded by the duration of each step.</returns>
    /// <remarks>
    /// An install command that exits with a non-zero code, such as when the package mirrors of <c>--with-deps</c> do not
    /// resolve, runs again after 5 seconds and once more after 15 seconds; the third failure fails the installation.
    /// Nothing else is retried. The waits count toward <paramref name="timeout"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The build output has no <c>playwright.ps1</c>.</exception>
    /// <exception cref="ProcessExitException">The install command exited with a non-zero code on every attempt; the last failure is thrown.</exception>
    /// <exception cref="TimeoutException">The installation did not finish within <paramref name="timeout"/>; its process is stopped first.</exception>
    public static async Task<string> Run(
        string root,
        string project,
        IEnumerable<string> browsers,
        bool withDependencies,
        string? configuration,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        TextWriter log,
        CancellationToken ct,
        TimeSpan? timeout = null,
        bool onlyShell = false,
        TimeProvider? timeProvider = null)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutSource.CancelAfter(timeout ?? DefaultTimeout);
        try
        {
            return await Install(
                root, project, browsers, withDependencies, onlyShell, configuration, run, log, timeProvider ?? TimeProvider.System, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Installing the Playwright browsers for {project} did not finish within {timeout ?? DefaultTimeout}.", exception);
        }
    }

    private static async Task<string> Install(
        string root,
        string project,
        IEnumerable<string> browsers,
        bool withDependencies,
        bool onlyShell,
        string? configuration,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        TextWriter log,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var projectPath = Path.GetFullPath(project, root);
        string[] evaluate = ["msbuild", projectPath, "-getProperty:OutputPath", .. configuration is null ? Array.Empty<string>() : ["-p:Configuration=" + configuration]];
        var evaluated = await run("dotnet", evaluate, root, ct).ConfigureAwait(false);
        var evaluation = Stopwatch.GetElapsedTime(started);
        // The property is the last line: the dotnet CLI can print banner lines first, such as the telemetry line under DOTNET_CLI_CONTEXT_VERBOSE.
        var outputPath = (evaluated.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var script = outputPath.Length == 0 ? string.Empty : Path.Combine(Path.GetFullPath(outputPath, Path.GetDirectoryName(projectPath)!), "playwright.ps1");
        if (!File.Exists(script))
        {
            throw new InvalidOperationException("playwright.ps1 is not in the build output of " + project + "; build it first.");
        }

        string[] install = [script, "install", .. withDependencies ? ["--with-deps"] : Array.Empty<string>(), .. onlyShell ? ["--only-shell"] : Array.Empty<string>(), .. browsers];
        var installStarted = Stopwatch.GetTimestamp();
        var output = await InstallWithRetries(install, root, run, log, timeProvider, ct).ConfigureAwait(false);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"playwright: msbuild evaluation took {evaluation.TotalSeconds:F1}s; install took {Stopwatch.GetElapsedTime(installStarted).TotalSeconds:F1}s{Environment.NewLine}{output}");
    }

    private static async Task<string> InstallWithRetries(
        string[] install,
        string root,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        TextWriter log,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var attempts = RetryDelays.Length + 1;
        for (var attempt = 1; ; attempt++)
        {
            TimeSpan delay;
            try
            {
                return await run("pwsh", install, root, ct).ConfigureAwait(false);
            }
            catch (ProcessExitException exception) when (attempt < attempts)
            {
                delay = RetryDelays[attempt - 1];
                await log.WriteLineAsync(string.Create(
                    CultureInfo.InvariantCulture,
                    $"playwright: install attempt {attempt} of {attempts} failed with exit code {exception.ExitCode}; retrying in {delay.TotalSeconds}s.")).ConfigureAwait(false);
            }

            await Task.Delay(delay, timeProvider, ct).ConfigureAwait(false);
        }
    }
}
