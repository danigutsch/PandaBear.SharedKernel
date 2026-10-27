using System.Globalization;
using System.Runtime.Versioning;

namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// A repository with a dev container definition, a fake Dev Container CLI, and a fake Docker that record their
/// arguments, one <c>[argument]</c> group per call.
/// </summary>
[UnsupportedOSPlatform("windows")]
internal sealed class DevcontainerTestWorkspace : IDisposable
{
    private readonly TemporaryDirectory _tools = new();
    private readonly TemporaryDirectory? _root;

    /// <summary>Creates the workspace in a new directory, or in <paramref name="rootPath"/> when it is given.</summary>
    public DevcontainerTestWorkspace(string? rootPath = null)
    {
        if (rootPath is null)
        {
            _root = new TemporaryDirectory();
            rootPath = _root.Path;
        }

        RootPath = rootPath;
        Directory.CreateDirectory(Path.Combine(RootPath, ".devcontainer"));
        File.WriteAllText(Path.Combine(RootPath, ".devcontainer", "devcontainer.json"), "{}");
        CliPath = Path.Combine(_tools.Path, "devcontainer");
        DockerPath = Path.Combine(_tools.Path, "docker");
        CliCallsPath = Path.Combine(_tools.Path, "cli-calls.txt");
        DockerCallsPath = Path.Combine(_tools.Path, "docker-calls.txt");
        WriteExecutable(DockerPath, "#!/bin/sh\n" + RecordCall(DockerCallsPath) + "\n");
        WriteCli(UpScript("{\"outcome\":\"success\",\"containerId\":\"abc123\"}"));
    }

    /// <summary>The workspace root directory.</summary>
    public string RootPath
    {
        get;
    }

    /// <summary>The directory that holds the fake Dev Container CLI, the fake Docker, and their call logs.</summary>
    public string ToolsPath => _tools.Path;

    /// <summary>The path of the fake Dev Container CLI executable.</summary>
    public string CliPath
    {
        get;
    }

    /// <summary>The path of the fake Docker executable.</summary>
    public string DockerPath
    {
        get;
    }

    /// <summary>The file that records each call to the fake Dev Container CLI.</summary>
    public string CliCallsPath
    {
        get;
    }

    /// <summary>The file that records each call to the fake Docker.</summary>
    public string DockerCallsPath
    {
        get;
    }

    /// <summary>Returns a shell fragment for <c>up</c> that prints <paramref name="stdout"/> and exits with <paramref name="exitCode"/>.</summary>
    public static string UpScript(string stdout, int exitCode = 0) =>
        $"printf '%s\\n' '{stdout}'; echo 'trace on stderr' >&2; exit {exitCode.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Writes a shell script for a fake Dev Container CLI that records its call, then runs the fragment for its subcommand.</summary>
    public string CliScript(string up, string exec = "echo \"exec output\"; exit 0") =>
        $$"""
        #!/bin/sh
        case "$1" in *devcontainer.js) shift ;; esac
        {{RecordCall(CliCallsPath)}}
        case "$1" in
          up) {{up}} ;;
          exec) {{exec}} ;;
        esac
        exit 64
        """;

    /// <summary>Replaces the fake Dev Container CLI with a script that runs the given <c>up</c> and <c>exec</c> fragments.</summary>
    public void WriteCli(string up, string exec = "echo \"exec output\"; exit 0") =>
        WriteExecutable(CliPath, CliScript(up, exec));

    /// <summary>Returns the recorded Dev Container CLI calls, one line per call.</summary>
    public string[] CliCalls() => ReadCalls(CliCallsPath);

    /// <summary>Returns the recorded Docker calls, one line per call.</summary>
    public string[] DockerCalls() => ReadCalls(DockerCallsPath);

    /// <summary>Creates a host that uses the fake Docker and the given platform, client, and pins.</summary>
    public DevcontainerHost Host(bool isLinuxX64 = true, HttpClient? httpClient = null, DevcontainerCliPins? pins = null) =>
        new(DockerPath, isLinuxX64, httpClient, pins ?? DevcontainerCliPins.Default);

    /// <summary>Runs <c>verify-devcontainer</c> with the given arguments against the workspace and returns its exit code, output, and error.</summary>
    public async Task<(int ExitCode, string Output, string Error)> Run(
        IReadOnlyList<string> arguments,
        DevcontainerHost? host = null,
        CancellationToken? cancellationToken = null)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = await RepoConfigToolRunner.Run(
            ["verify-devcontainer", .. arguments],
            output,
            error,
            new RepoConfigToolEnvironment(RootPath) { DevcontainerHost = host ?? Host() },
            cancellationToken ?? TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Writes the content with LF line endings and marks the file executable for its owner.</summary>
    public static void WriteExecutable(string path, string content)
    {
        File.WriteAllText(path, content.ReplaceLineEndings("\n"));
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>Deletes the temporary tools directory and the root directory when the workspace created it.</summary>
    public void Dispose()
    {
        _tools.Dispose();
        _root?.Dispose();
    }

    private static string RecordCall(string path) =>
        $"for argument; do printf '[%s]' \"$argument\"; done >> '{path}'; echo >> '{path}'";

    private static string[] ReadCalls(string path) =>
        File.Exists(path) ? File.ReadAllLines(path) : [];
}
