using System.CommandLine;
using System.Globalization;

namespace PandaBear.Packages.Tool.Tests;

/// <summary>Runs the tool in a temporary repository with fake package tools.</summary>
internal static class PackagesToolHarness
{
    /// <summary>Resolves no tool at all.</summary>
    public static string? ResolveNone(string command) => null;

    /// <summary>Resolves every known tool to a fake absolute path.</summary>
    public static string? ResolveAll(string command) => "/fake/" + command;

    /// <summary>Resolves every known tool except cargo.</summary>
    public static string? ResolveWithoutCargo(string command) => command == "cargo" ? null : "/fake/" + command;

    /// <summary>Writes <paramref name="configuration"/> as the repository's configuration file and returns the directory.</summary>
    public static TemporaryDirectory CreateRepository(string? configuration)
    {
        var directory = new TemporaryDirectory();
        if (configuration is not null)
        {
            Directory.CreateDirectory(Path.Combine(directory.Path, ".config"));
            File.WriteAllText(Path.Combine(directory.Path, ".config", "sharedkernel-packages.json"), configuration);
        }

        return directory;
    }

    /// <summary>Runs the command line against <paramref name="root"/> and returns the exit code, output, and error.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> Run(
        string root,
        IProcessRunner runner,
        Func<string, string?> resolve,
        params string[] arguments)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = await PackagesCommandLine
            .Create(root, resolve, runner)
            .Parse(["plan", "--root", root, .. arguments])
            .InvokeAsync(new InvocationConfiguration { Output = output, Error = error, ProcessTerminationTimeout = null }, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Runs the command line with exactly <paramref name="arguments"/>, resolving a relative <c>--root</c> and the default root from <paramref name="workingDirectory"/>.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> RunWithArguments(
        string workingDirectory,
        IProcessRunner runner,
        Func<string, string?> resolve,
        params string[] arguments)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = await PackagesCommandLine
            .Create(workingDirectory, resolve, runner)
            .Parse(arguments)
            .InvokeAsync(new InvocationConfiguration { Output = output, Error = error, ProcessTerminationTimeout = null }, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }
}
