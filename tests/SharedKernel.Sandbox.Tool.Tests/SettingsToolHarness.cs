using System.CommandLine;
using System.Globalization;
using System.Text.Json.Nodes;

namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>Runs <c>settings</c> commands in a temporary repository and reads the result.</summary>
internal static class SettingsToolHarness
{
    /// <summary>The settings file path used by most tests, relative to the root.</summary>
    public const string DefaultTarget = ".claude/settings.json";

    /// <summary>Builds the configuration text with a <c>settings</c> section.</summary>
    public static string Configuration(string settingsJson, string deniedReadPaths = "[]") =>
        $$"""{ "version": 1, "deniedReadPaths": {{deniedReadPaths}}, "settings": {{settingsJson}} }""";

    /// <summary>Creates a repository with <paramref name="configuration"/> and, when given, the settings file <paramref name="settings"/> at <paramref name="target"/>.</summary>
    public static TemporaryDirectory CreateRepository(string configuration, string? settings = null, string target = DefaultTarget)
    {
        var repository = SandboxToolHarness.CreateRepository(configuration);
        if (settings is not null)
        {
            var path = Path.Combine(repository.Path, target);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, settings);
        }

        return repository;
    }

    /// <summary>Runs the <c>settings</c> command line with <paramref name="arguments"/> against <paramref name="root"/>.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> Run(string root, Action? beforeCommit, params string[] arguments) =>
        await Run(root, new FakeSandboxEnvironment(), beforeCommit, arguments);

    /// <summary>Runs the <c>settings</c> command line with <paramref name="arguments"/> in <paramref name="environment"/>.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> Run(string root, FakeSandboxEnvironment environment, Action? beforeCommit, params string[] arguments)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = await SandboxCommandLine
            .Create(root, environment, beforeCommit)
            .Parse(["settings", .. arguments, "--root", root])
            .InvokeAsync(new InvocationConfiguration { Output = output, Error = error, ProcessTerminationTimeout = null }, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Runs the <c>settings</c> command line with <paramref name="arguments"/> against <paramref name="root"/>.</summary>
    public static Task<(int ExitCode, string Output, string Error)> Run(string root, params string[] arguments) => Run(root, null, arguments);

    /// <summary>Reads the string list at <paramref name="path"/> in the settings file.</summary>
    public static string[] List(string root, string target, params string[] path)
    {
        JsonNode? node = JsonNode.Parse(File.ReadAllText(Path.Combine(root, target)));
        foreach (var segment in path)
        {
            node = node?[segment];
        }

        return node is JsonArray list ? [.. list.Select(static item => item!.GetValue<string>())] : [];
    }

    /// <summary>Reads a file as text.</summary>
    public static string ReadText(string path) => File.ReadAllText(path);

    /// <summary>Reads a file as bytes.</summary>
    public static byte[] ReadBytes(string path) => File.ReadAllBytes(path);

    /// <summary>Replaces a file's text.</summary>
    public static void WriteText(string path, string text) => File.WriteAllText(path, text);

    /// <summary>Lists the names in the directory of the settings file.</summary>
    public static string[] Siblings(string root, string target) =>
        [.. Directory.GetFileSystemEntries(Path.GetDirectoryName(Path.Combine(root, target))!).Select(static entry => Path.GetFileName(entry)).Order(StringComparer.Ordinal)];
}
