using System.Diagnostics;
using PandaBear.Tooling;

namespace PandaBear.Repository.Tests;

/// <summary>
/// Runs the built CI tool with <c>dotnet exec</c> in its own process, which has its own working directory and
/// environment.
/// </summary>
internal static class CiToolProcess
{
    /// <summary>
    /// Runs the tool with <paramref name="environment"/> added to the inherited environment and waits at most 60 seconds
    /// for it to exit.
    /// </summary>
    public static async Task<RepositoryToolResult> Run(
        string workingDirectory,
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken ct)
    {
        using var tool = Start(workingDirectory, args, environment);
        return await tool.Exited(ct);
    }

    /// <summary>Starts the tool with <paramref name="environment"/> added to the inherited environment and returns without waiting for it.</summary>
    public static RunningCiTool Start(
        string workingDirectory,
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string?> environment)
    {
        // The test process resolves dotnet, so a dotnet on the PATH that the tool gets cannot stand in for the host.
        var startInfo = new ProcessStartInfo(ExecutableResolver.ResolveCommand("dotnet"))
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "SharedKernel.CI.Tool.dll"));
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        return new RunningCiTool(Process.Start(startInfo) ?? throw new InvalidOperationException("The CI tool did not start."));
    }
}
