namespace PandaBear.Sandbox.Tool;

/// <summary>
/// The built-in <c>dotnet</c> profile: what a .NET build needs from a sandbox. Everything that widens access beyond a plain
/// restore is opt-in through the configuration.
/// </summary>
internal static class DotnetSettingsProfile
{
    /// <summary>The profile name in the configuration.</summary>
    public const string Name = "dotnet";

    /// <summary>The domain a plain restore from nuget.org needs: the service index, the package registrations, the vulnerability feed, and the package downloads are all served from it (observed, not documented).</summary>
    public const string FeedDomain = "api.nuget.org";

    /// <summary>The domain that hosts SDK downloads; opt-in. Not verified against every install script.</summary>
    public const string SdkDomain = "builds.dotnet.microsoft.com";

    /// <summary>The package folders the NuGet client writes to on Linux.</summary>
    public static readonly string[] NuGetFolders = ["~/.nuget/packages", "~/.local/share/NuGet"];

    /// <summary>The folder of the dotnet host's per-user data.</summary>
    public const string DotnetHome = "~/.dotnet";

    /// <summary>The variables that keep build servers and telemetry from outliving a sandboxed command.</summary>
    public static readonly IReadOnlyDictionary<string, string> Environment = new Dictionary<string, string>
    {
        ["MSBUILDDISABLENODEREUSE"] = "1",
        ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
    };
}
