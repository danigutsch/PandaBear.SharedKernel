using System.Diagnostics;

namespace PandaBear.Testing.Tests;

/// <summary>Builds start information for the process fake tests.</summary>
internal static class ProcessStarts
{
    /// <summary>Returns start information for <paramref name="fileName"/> with <paramref name="arguments"/>.</summary>
    public static ProcessStartInfo Of(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
