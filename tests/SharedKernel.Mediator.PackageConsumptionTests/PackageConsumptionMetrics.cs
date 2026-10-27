using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PandaBear.Mediator.PackageConsumptionTests;

/// <summary>Helpers that measure publish output and run published consumer executables in the package consumption tests.</summary>
internal static partial class PackageConsumptionMetrics
{
    /// <summary>Counts the 'warning ILnnnn' trim warnings in the publish output.</summary>
    public static int CountTrimWarnings(string publishOutput)
    {
        return TrimWarningRegex().Count(publishOutput);
    }

    /// <summary>Sums the size in bytes of the distinct generated AppMediator, DependencyInjection, DiscoveryReport and GeneratedDispatch source files found in the workspace.</summary>
    public static long GetGeneratedSourceSize(PackageConsumptionWorkspace workspace)
    {
        var generatedFiles = new[]
        {
            "SharedKernel.Mediator.Generated.AppMediator.g.cs",
            "SharedKernel.Mediator.Generated.DependencyInjection.g.cs",
            "SharedKernel.Mediator.Generated.DiscoveryReport.g.cs",
            "SharedKernel.Mediator.Generated.GeneratedDispatch.g.cs",
        };

        return generatedFiles
            .SelectMany(workspace.GetGeneratedFiles)
            .Distinct(StringComparer.Ordinal)
            .Sum(static file => new FileInfo(file).Length);
    }

    /// <summary>Returns the runtime identifier of the current machine, such as linux-x64 or win-arm64, throwing for architectures other than x64 and Arm64.</summary>
    public static string GetCurrentRuntimeIdentifier()
    {
        var os = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows), RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) switch
        {
            (true, _) => "win",
            (_, true) => "osx",
            _ => "linux",
        };
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            _ => throw new InvalidOperationException(
                $"Unsupported architecture for publish validation: {RuntimeInformation.ProcessArchitecture}."),
        };

        return $"{os}-{architecture}";
    }

    /// <summary>Reads the first-dispatch-ms and steady-state-dispatch-ms lines from the output into time spans, throwing when either is missing.</summary>
    public static (TimeSpan FirstDispatch, TimeSpan SteadyStateDispatch) ParseRuntimeMetrics(string output)
    {
        double? firstDispatchMilliseconds = null;
        double? steadyStateMilliseconds = null;

        foreach (var line in output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("first-dispatch-ms=", StringComparison.Ordinal))
            {
                firstDispatchMilliseconds = double.Parse(
                    line["first-dispatch-ms=".Length..],
                    CultureInfo.InvariantCulture);
            }

            if (line.StartsWith("steady-state-dispatch-ms=", StringComparison.Ordinal))
            {
                steadyStateMilliseconds = double.Parse(
                    line["steady-state-dispatch-ms=".Length..],
                    CultureInfo.InvariantCulture);
            }
        }

        return firstDispatchMilliseconds is null || steadyStateMilliseconds is null
            ? throw new InvalidOperationException($"Runtime metrics output was incomplete:{Environment.NewLine}{output}")
            : (TimeSpan.FromMilliseconds(firstDispatchMilliseconds.Value), TimeSpan.FromMilliseconds(steadyStateMilliseconds.Value));
    }

    /// <summary>Runs the published executable in its own directory, captures stdout and stderr with the elapsed time, and throws when the exit code is not 0.</summary>
    public static async Task<(string Output, TimeSpan Duration)> RunPublishedExecutable(string publishedExecutable)
    {
        var workingDirectory = Path.GetDirectoryName(publishedExecutable)
            ?? throw new InvalidOperationException($"Could not determine directory for '{publishedExecutable}'.");

        var startInfo = new ProcessStartInfo
        {
            FileName = publishedExecutable,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start published executable '{publishedExecutable}'.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        var output = string.Concat(await stdoutTask, await stderrTask);

        return process.ExitCode != 0
            ? throw new InvalidOperationException(
                $"Published executable '{publishedExecutable}' failed with exit code {process.ExitCode}.{Environment.NewLine}{output}")
            : (output, stopwatch.Elapsed);
    }

    [GeneratedRegex(@"warning IL\d{4}", RegexOptions.CultureInvariant)]
    private static partial Regex TrimWarningRegex();
}
