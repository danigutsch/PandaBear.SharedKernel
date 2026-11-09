using System.CommandLine;
using System.Globalization;

namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>Runs the tool with a fake environment in a temporary repository.</summary>
internal static class SandboxToolHarness
{
    /// <summary>Creates a temporary repository, with <paramref name="configuration"/> as its configuration file when given.</summary>
    public static TemporaryDirectory CreateRepository(string? configuration)
    {
        var directory = new TemporaryDirectory();
        if (configuration is not null)
        {
            Directory.CreateDirectory(Path.Combine(directory.Path, ".config"));
            File.WriteAllText(Path.Combine(directory.Path, ".config", "sharedkernel-sandbox.json"), configuration);
        }

        return directory;
    }

    /// <summary>Runs <c>doctor</c> against <paramref name="root"/> and returns the exit code, output, and error.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> Run(string root, ISandboxEnvironment environment)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = await SandboxCommandLine
            .Create(root, environment)
            .Parse(["doctor", "--root", root])
            .InvokeAsync(new InvocationConfiguration { Output = output, Error = error, ProcessTerminationTimeout = null }, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }
}
