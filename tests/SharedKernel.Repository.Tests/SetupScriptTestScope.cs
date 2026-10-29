using System.Globalization;
using PandaBear.RepoConfig.Tests;

namespace PandaBear.Repository.Tests;

/// <summary>A temporary directory that holds a copy of the setup script and a fake <c>dotnet</c> that logs each call and exits with a configured code.</summary>
internal sealed class SetupScriptTestScope : IDisposable
{
    private readonly TemporaryDirectory directory = new();
    private readonly string tools;
    private readonly string outside;
    private readonly string script;

    /// <summary>Copies setup-dev.sh, or setup-dev.ps1 on Windows, into a root whose name contains a space and writes a fake <c>dotnet</c> that logs its directory and arguments and exits with the code in the environment for restore or build.</summary>
    public SetupScriptTestScope()
    {
        Root = Path.Combine(directory.Path, "sharedkernel setup");
        tools = Path.Combine(Root, "test tools");
        outside = Path.Combine(Root, "outside checkout");
        Directory.CreateDirectory(tools);
        Directory.CreateDirectory(outside);
        LogPath = Path.Combine(Root, "dotnet-calls.txt");
        var scriptName = OperatingSystem.IsWindows() ? "setup-dev.ps1" : "setup-dev.sh";
        script = Path.Combine(Root, scriptName);
        File.Copy(Path.Combine(CiTestSelectionTestProcess.GetRepositoryRoot(), scriptName), script);

        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(Path.Combine(tools, "dotnet.cmd"), """
                @echo off
                >>"%SETUP_TEST_LOG%" echo %CD%^|%*
                if "%~1"=="restore" exit /b %SETUP_TEST_RESTORE_EXIT%
                if "%~1"=="build" exit /b %SETUP_TEST_BUILD_EXIT%
                exit /b 99
                """.Replace("\n", "\r\n", StringComparison.Ordinal));
            return;
        }

        var fakeDotnet = Path.Combine(tools, "dotnet");
        File.WriteAllText(fakeDotnet, """
            #!/bin/sh
            printf '%s|%s\n' "$PWD" "$*" >> "$SETUP_TEST_LOG"
            case "$1" in
              restore) exit "$SETUP_TEST_RESTORE_EXIT" ;;
              build) exit "$SETUP_TEST_BUILD_EXIT" ;;
              *) exit 99 ;;
            esac
            """);
        File.SetUnixFileMode(fakeDotnet, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>Gets the directory that holds the copied script, which the script must treat as the repository root.</summary>
    public string Root
    {
        get;
    }

    /// <summary>Gets the file to which the fake <c>dotnet</c> appends one line per call, as the working directory and the arguments.</summary>
    public string LogPath
    {
        get;
    }

    /// <summary>Runs the script from a directory outside the checkout with the fake <c>dotnet</c> first on PATH, so restore and build exit with the given codes.</summary>
    public Task<TestProcessResult> Run(int restoreExitCode, int buildExitCode, CancellationToken ct)
    {
        string[] arguments = OperatingSystem.IsWindows()
            ? ["-NoLogo", "-NoProfile", "-NonInteractive", "-File", script]
            : [script];
        return TestProcess.Run(
            OperatingSystem.IsWindows() ? "pwsh" : "bash",
            outside,
            arguments,
            ct,
            new Dictionary<string, string?>
            {
                ["PATH"] = tools + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
                ["SETUP_TEST_LOG"] = LogPath,
                ["SETUP_TEST_RESTORE_EXIT"] = restoreExitCode.ToString(CultureInfo.InvariantCulture),
                ["SETUP_TEST_BUILD_EXIT"] = buildExitCode.ToString(CultureInfo.InvariantCulture),
                ["BASH_ENV"] = null,
                ["ENV"] = null
            });
    }

    /// <inheritdoc />
    public void Dispose() => directory.Dispose();
}
