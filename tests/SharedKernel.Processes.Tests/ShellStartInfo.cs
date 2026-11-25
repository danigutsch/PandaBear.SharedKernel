using System.Diagnostics;

namespace SharedKernel.Processes.Tests;

/// <summary>Describes a <c>/bin/sh -c</c> run of a script.</summary>
internal static class ShellStartInfo
{
    /// <summary>Returns the start information that runs <paramref name="script"/> with <c>/bin/sh</c>.</summary>
    public static ProcessStartInfo Create(string script)
    {
        var startInfo = new ProcessStartInfo("/bin/sh");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(script);
        return startInfo;
    }
}
