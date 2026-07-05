using System.Diagnostics;
using System.Text;

namespace SharedKernel.Versioning.Tool;

internal static class CommandRunner
{
    public static string Run(string fileName, IEnumerable<string> arguments, string? workingDirectory = null, string? standardInput = null)
    {
        var argumentList = arguments.ToArray();
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var argument in argumentList)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
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

        if (process.ExitCode != 0)
        {
            var command = new StringBuilder(fileName);
            foreach (var argument in argumentList)
            {
                command.Append(' ').Append(argument);
            }

            throw new InvalidOperationException($"Command failed ({process.ExitCode}): {command}{Environment.NewLine}{error}");
        }

        return output;
    }

    public static string RunOrDefault(string fileName, IEnumerable<string> arguments, string? workingDirectory = null)
    {
        try
        {
            return Run(fileName, arguments, workingDirectory);
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }
}
