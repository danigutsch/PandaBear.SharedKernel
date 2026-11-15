namespace PandaBear.CI;

/// <summary>Installs Playwright browsers with the <c>playwright.ps1</c> in the build output of a project, found through its evaluated <c>OutputPath</c>.</summary>
public sealed class PlaywrightInstaller
{
    private readonly CiMachine _machine;

    /// <summary>Creates the installer from the machine that runs dotnet and PowerShell.</summary>
    /// <param name="machine">Runs the evaluation and the installer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="machine"/> is <see langword="null"/>.</exception>
    public PlaywrightInstaller(CiMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
    }

    /// <summary>
    /// Runs the install command of the project's <c>playwright.ps1</c>, writing each installer output line to <paramref name="progress"/>
    /// with its UTC time as it arrives, and stops it after fifteen minutes.
    /// </summary>
    /// <param name="root">The repository root, the working directory of both processes.</param>
    /// <param name="project">The built project, relative to the root.</param>
    /// <param name="browsers">The browsers, such as <c>chromium</c>; none installs Playwright's default browsers.</param>
    /// <param name="withDependencies">Whether to also install the browsers' operating system dependencies.</param>
    /// <param name="onlyShell">Whether to install only the headless shell of Chromium, not the full browser.</param>
    /// <param name="configuration">The configuration whose output holds the installer, or <see langword="null"/> for the project default.</param>
    /// <param name="progress">Receives the installer's output as it arrives.</param>
    /// <param name="ct">Cancels the processes.</param>
    /// <returns>The installer's output, preceded by the duration of each step.</returns>
    /// <exception cref="InvalidOperationException">The build output has no <c>playwright.ps1</c>, or a process failed.</exception>
    /// <exception cref="TimeoutException">The installation did not finish in time; its process is stopped first.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task<string> Install(
        string root,
        string project,
        IEnumerable<string> browsers,
        bool withDependencies,
        bool onlyShell,
        string? configuration,
        TextWriter progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(browsers);
        ArgumentNullException.ThrowIfNull(progress);
        return PlaywrightInstallation.Run(
            root,
            project,
            browsers,
            withDependencies,
            configuration,
            _machine.Run,
            ct,
            onlyShell: onlyShell,
            runInstaller: (fileName, arguments, directory, token) => _machine.RunStreaming(fileName, arguments, directory, progress, token));
    }
}
