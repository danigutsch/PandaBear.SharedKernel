using System.Diagnostics;
using System.Text;

namespace SharedKernel.Mediator.PackageConsumptionTests;

/// <summary>
/// Runs focused dotnet CLI commands for package-consumption tests.
/// </summary>
internal static class DotNetCli
{
    /// <summary>
    /// Executes a dotnet CLI command and throws when the command fails.
    /// </summary>
    /// <param name="workingDirectory">The working directory used for the command.</param>
    /// <param name="arguments">The arguments passed to dotnet.</param>
    /// <returns>The combined standard output and error text.</returns>
    public static async Task<string> RunAsync(string workingDirectory, params string[] arguments)
    {
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
