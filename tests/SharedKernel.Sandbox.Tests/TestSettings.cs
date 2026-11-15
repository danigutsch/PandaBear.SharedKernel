namespace PandaBear.Sandbox.Tests;

/// <summary>Builds desired settings with only the entries a test names.</summary>
internal static class TestSettings
{
    /// <summary>Returns desired settings with the given entries and every other list empty.</summary>
    public static DesiredSettings Desired(
        string[]? allowWrite = null,
        string[]? denyRead = null,
        string[]? domains = null,
        string[]? excluded = null,
        string[]? removeDenyRead = null,
        bool sockets = false,
        Dictionary<string, string>? environment = null) =>
        new(null, allowWrite ?? [], [], denyRead ?? [], domains ?? [], excluded ?? [], removeDenyRead ?? [], sockets, environment ?? [], []);
}
