using System.Runtime.InteropServices;

namespace PandaBear.Repository.Tests;

/// <summary>The dotnet host that runs the tests, found as the tool finds the host that runs it.</summary>
internal static class DotnetHost
{
    /// <summary>Gets the absolute path of the dotnet executable of the running runtime.</summary>
    public static string Path { get; } = System.IO.Path.GetFullPath(System.IO.Path.Combine(
        RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
}
