using System.Diagnostics;
using System.Text;

namespace PandaBear.Versioning.Tool;

internal static class CommandRunner
{
    public static string Run(string fileName, IEnumerable<string> arguments, string? workingDirectory = null, string? standardInput = null)
    {
        var argumentList = arguments.ToArray();
        using var process = StartProcess(CreateStartInfo(fileName, argumentList, workingDirectory, standardInput is not null), fileName);
        if (standardInput is not null)
        {
            process.StandardInput.Write(standardInput);
            process.StandardInput.Close();
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();
        EnsureSuccess(process, fileName, argumentList, output, error);
        return output;
    }

    /// <summary>Runs a process and kills its process tree when <paramref name="ct" /> is canceled.</summary>
    public static async Task<string> Run(string fileName, IEnumerable<string> arguments, string? workingDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var argumentList = arguments.ToArray();
        using var process = StartProcess(CreateStartInfo(fileName, argumentList, workingDirectory, redirectStandardInput: false), fileName);
        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        EnsureSuccess(process, fileName, argumentList, output, error);
        return output;
    }

    public static async Task<string> RunOrDefault(string fileName, IEnumerable<string> arguments, string? workingDirectory, CancellationToken ct)
    {
        try
        {
            return await Run(fileName, arguments, workingDirectory, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, string[] arguments, string? workingDirectory, bool redirectStandardInput)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardError = true,
            RedirectStandardInput = redirectStandardInput,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void EnsureSuccess(Process process, string fileName, string[] arguments, string output, string error)
    {
        if (process.ExitCode == 0)
        {
            return;
        }

        var command = new StringBuilder(fileName);
        foreach (var argument in arguments)
        {
            command.Append(' ').Append(argument);
        }

        throw new InvalidOperationException($"Command failed ({process.ExitCode}): {command}{Environment.NewLine}{output}{Environment.NewLine}{error}");
    }

    private static Process StartProcess(ProcessStartInfo startInfo, string fileName)
    {
        try
        {
            return Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to start {fileName}.", ex);
        }
    }
}
