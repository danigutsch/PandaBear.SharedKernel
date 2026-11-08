using System.Globalization;
using System.Diagnostics;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Test helpers for the <c>new</c> command.</summary>
internal static class NewTestOperations
{
    /// <summary>Runs <c>new --root &lt;workspace&gt;</c> with <paramref name="options"/>.</summary>
    public static Task<(int ExitCode, string Output, string Error)> Run(TemporaryRepoConfigWorkspace workspace, params string[] options) =>
        RunAt(workspace.RootPath, options);

    /// <summary>Runs <c>new --root &lt;root&gt;</c> with <paramref name="options"/>.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> RunAt(string root, params string[] options)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        List<string> arguments = ["new", "--root", root, .. options];
        var exitCode = await RepoConfigToolRunner.Run(arguments, output, error, Path.GetTempPath(), TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Runs <c>baseline verify</c> against the workspace.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> BaselineVerify(TemporaryRepoConfigWorkspace workspace)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = await RepoConfigToolRunner.Run(["baseline", "verify", "--root", workspace.RootPath], output, error, Path.GetTempPath(), TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Reads a local Git setting of the workspace, or returns an empty string when it is not set.</summary>
    public static async Task<string> GitConfig(TemporaryRepoConfigWorkspace workspace, string key)
    {
        ProcessStartInfo startInfo = new("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workspace.RootPath,
        };
        foreach (var argument in (string[])["config", "--local", "--get", key])
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var text = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return text.Trim();
    }

    /// <summary>Asserts that a local Git setting of the workspace has the expected value; an empty value means it is not set.</summary>
    public static async Task GitConfigShouldBe(TemporaryRepoConfigWorkspace workspace, string key, string expected, string? because = null)
    {
        var actual = await GitConfig(workspace, key);
        actual.ShouldBe(expected, because ?? $"git config {key} has the expected value.");
    }
}
