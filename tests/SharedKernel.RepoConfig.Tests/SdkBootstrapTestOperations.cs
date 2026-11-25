using System.Diagnostics;
using System.Security.Cryptography;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Supplies synthetic shell tools and release data for an offline SDK bootstrap test.</summary>
internal static class SdkBootstrapTestOperations
{
    /// <summary>The expected trusted archive URL for the synthetic SDK and runtime identifier.</summary>
    internal const string OfficialArchiveUrl = "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.100/dotnet-sdk-10.0.100-linux-x64.tar.gz";

    /// <summary>The isolated versioned install directory used by the profile.</summary>
    internal const string InstallParent = "bin/sharedkernel-dotnet";

    /// <summary>The host file in the expected side-by-side installation.</summary>
    internal const string InstalledHost = InstallParent + "/10.0.100/linux-x64/dotnet";

    private const string ListSdk = "#!/bin/sh\n[ \"$#\" = 1 ] && [ \"$1\" = --list-sdks ] || exit 81\n";
    private const string InstalledDotnet = ListSdk + "printf '%s\\n' '10.0.100 [fixture]'\n";
    private const string MissingDotnet = ListSdk + "printf '%s\\n' '10.0.101 [fixture]'\n";

    /// <summary>The actual SHA-512 digest of the synthetic archive data.</summary>
    internal static string FixtureHash { get; } = Convert.ToHexStringLower(SHA512.HashData("SDK archive fixture."u8));

    /// <summary>Scaffolds a repository with the pinned SDK and bootstrap profile.</summary>
    internal static Task<(int ExitCode, string Output, string Error)> Scaffold(TemporaryRepoConfigWorkspace workspace) =>
        NewTestOperations.Run(workspace, "--language", "dotnet", "--sdk-version", "10.0.100", "--feature", "global-json", "--feature", "sdk-bootstrap", "--force");

    /// <summary>Provides synthetic programs without installing a real SDK or contacting a remote release feed.</summary>
    internal static void PrepareTools(TemporaryRepoConfigWorkspace workspace, TemporaryRepoConfigWorkspace tools, bool sdkInstalled)
    {
        workspace.WriteFile("fixture-archive", "SDK archive fixture.");
        workspace.WriteFile("fixture-extracted-host", InstalledDotnet);
        WriteExecutable(tools, "dotnet", sdkInstalled ? InstalledDotnet : MissingDotnet);
        WriteExecutable(tools, "uname", """
            #!/bin/sh
            case "$1" in
                -s) printf '%s\n' Linux ;;
                -m) printf '%s\n' x86_64 ;;
                *) exit 1 ;;
            esac
            """);
        WriteExecutable(tools, "ldd", "#!/bin/sh\nprintf '%s\\n' 'fixture glibc'\n");
        WriteExecutable(tools, "curl", """
            #!/bin/sh
            while [ "$#" -gt 0 ]; do
                case "$1" in
                    --output) output=$2; shift 2 ;;
                    https://*) url=$1; shift ;;
                    *) shift ;;
                esac
            done
            case "$url" in
                https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)
                    touch metadata-requested
                    cp fixture-release.json "$output"
                    ;;
                *) touch archive-requested; cp fixture-archive "$output" ;;
            esac
            """);
        WriteExecutable(tools, "tar", """
            #!/bin/sh
            touch extraction-attempted
            while [ "$#" -gt 0 ]; do
                case "$1" in
                    --directory) destination=$2; shift 2 ;;
                    *) shift ;;
                esac
            done
            cp fixture-extracted-host "$destination/dotnet"
            chmod u+x "$destination/dotnet"
            """);
    }

    /// <summary>Writes the exact SDK entry in the shape of the vendor release metadata.</summary>
    internal static void WriteReleaseFeed(TemporaryRepoConfigWorkspace workspace, string url, string hash) =>
        workspace.WriteFile("fixture-release.json", $$"""
            {
              "releases": [
                {
                  "sdk": { "version": "10.0.101", "files": [] },
                  "sdks": [
                    { "version": "10.0.100", "files": [
                      { "name": "dotnet-sdk-linux-x64.tar.gz", "rid": "linux-x64", "url": "{{url}}", "hash": "{{hash}}" }
                    ] }
                  ]
                }
              ]
            }
            """);

    /// <summary>Uses Git's shell to run the bootstrap script with the fixture tools first on its path.</summary>
    internal static async Task<(int ExitCode, string Output, string Error)> Run(
        TemporaryRepoConfigWorkspace workspace,
        TemporaryRepoConfigWorkspace tools,
        params string[] options)
    {
        ProcessStartInfo start = new("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workspace.RootPath,
        };
        foreach (var argument in (string[])["-c", "alias.run-bootstrap-fixture=!sh ./scripts/bootstrap-dotnet.sh", "run-bootstrap-fixture", .. options])
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["PATH"] = tools.RootPath + Path.PathSeparator + start.Environment["PATH"];
        using var process = Process.Start(start)!;
        var ct = TestContext.Current.CancellationToken;
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, await output, await error);
    }

    private static void WriteExecutable(TemporaryRepoConfigWorkspace tools, string filename, string script)
    {
        tools.WriteFile(filename, script.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Path.Combine(tools.RootPath, filename), UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
    }
}
