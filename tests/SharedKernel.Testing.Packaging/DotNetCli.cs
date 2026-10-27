using System.Diagnostics;
using System.Text;

namespace PandaBear.Testing.Packaging;

/// <summary>
/// Runs focused dotnet CLI commands for package-consumption tests.
/// </summary>
public static class DotNetCli
{
    private static readonly string PackLockPath = Path.Combine(Path.GetTempPath(), "sharedkernel-package-consumption-pack.lock");

    /// <summary>
    /// Packs a repository project into a feed directory while holding a machine-wide lock.
    /// </summary>
    /// <remarks>
    /// <c>dotnet pack</c> rebuilds the project and its references into the shared repository output directories.
    /// Test modules run in parallel processes, so concurrent packs of the same project race on files such as <c>*.deps.json</c>.
    /// The lock lets one pack own those directories at a time.
    /// </remarks>
    /// <param name="repositoryRoot">The working directory used for the command.</param>
    /// <param name="projectPath">The project to pack.</param>
    /// <param name="packageVersion">The version assigned to the package.</param>
    /// <param name="feedPath">The output directory that receives the package.</param>
    /// <returns>The combined standard output and error text.</returns>
    public static async Task<string> Pack(string repositoryRoot, string projectPath, string packageVersion, string feedPath)
    {
        await using var packLock = await AcquirePackLock().ConfigureAwait(false);
        return await Run(
                repositoryRoot,
                "pack",
                projectPath,
                "-c",
                "Release",
                "-p:PackageVersion=" + packageVersion,
                "-o",
                feedPath,
                "--nologo")
            .ConfigureAwait(false);
    }

    private static async Task<FileStream> AcquirePackLock()
    {
        while (true)
        {
            try
            {
                return new FileStream(PackLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Executes a dotnet CLI command and throws when the command fails.
    /// </summary>
    /// <param name="workingDirectory">The working directory used for the command.</param>
    /// <param name="arguments">The arguments passed to dotnet.</param>
    /// <returns>The combined standard output and error text.</returns>
    public static async Task<string> Run(string workingDirectory, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        var outputBuilder = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                outputBuilder.AppendLine(eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                outputBuilder.AppendLine(eventArgs.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start dotnet process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync().ConfigureAwait(false);

        var output = outputBuilder.ToString();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.{Environment.NewLine}{output}");
        }

        return output;
    }
}
