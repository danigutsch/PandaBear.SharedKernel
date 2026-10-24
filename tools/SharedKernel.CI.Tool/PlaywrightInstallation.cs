namespace PandaBear.CI.Tool;

/// <summary>Installs Playwright browsers with the <c>playwright.ps1</c> that a built project's output holds.</summary>
internal static class PlaywrightInstallation
{
    /// <summary>Finds <c>playwright.ps1</c> through the evaluated <c>OutputPath</c> of a project and runs its install command.</summary>
    /// <param name="root">The repository root, the working directory of both processes.</param>
    /// <param name="project">The project, relative to the root.</param>
    /// <param name="browsers">The browsers; none installs Playwright's default browsers.</param>
    /// <param name="withDependencies">Whether to also install the browsers' operating system dependencies.</param>
    /// <param name="configuration">The configuration whose output holds the installer, or <see langword="null"/> for the project default.</param>
    /// <param name="run">Runs a child process and returns its standard output.</param>
    /// <param name="ct">Cancels the processes.</param>
    /// <returns>The installer's standard output.</returns>
    /// <exception cref="InvalidOperationException">The build output has no <c>playwright.ps1</c>.</exception>
    public static async Task<string> Run(
        string root,
        string project,
        IEnumerable<string> browsers,
        bool withDependencies,
        string? configuration,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        CancellationToken ct)
    {
        var projectPath = Path.GetFullPath(project, root);
        string[] evaluate = ["msbuild", projectPath, "-getProperty:OutputPath", .. configuration is null ? Array.Empty<string>() : ["-p:Configuration=" + configuration]];
        var outputPath = (await run("dotnet", evaluate, root, ct).ConfigureAwait(false)).Trim()
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var script = outputPath.Length == 0 ? string.Empty : Path.Combine(Path.GetFullPath(outputPath, Path.GetDirectoryName(projectPath)!), "playwright.ps1");
        if (!File.Exists(script))
        {
            throw new InvalidOperationException("playwright.ps1 is not in the build output of " + project + "; build it first.");
        }

        string[] install = [script, "install", .. withDependencies ? ["--with-deps"] : Array.Empty<string>(), .. browsers];
        return await run("pwsh", install, root, ct).ConfigureAwait(false);
    }
}
