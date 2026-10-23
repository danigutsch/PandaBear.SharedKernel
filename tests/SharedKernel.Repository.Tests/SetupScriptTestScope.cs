using System.Globalization;
using PandaBear.RepoConfig.Tests;
using PandaBear.Testing;

namespace PandaBear.Repository.Tests;

internal sealed class SetupScriptTestScope : IDisposable
{
    private readonly TemporaryDirectory directory = new();
    private readonly string tools;
    private readonly string outside;
    private readonly string script;

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

    public string Root
    {
        get;
    }

    public string LogPath
    {
        get;
    }

    public Task<CiTestSelectionTestProcess.ProcessResult> Run(int restoreExitCode, int buildExitCode, CancellationToken ct)
    {
        string[] arguments = OperatingSystem.IsWindows()
            ? ["-NoLogo", "-NoProfile", "-NonInteractive", "-File", script]
            : [script];
        return CiTestSelectionTestProcess.Run(
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

    public void Dispose() => directory.Dispose();
}
