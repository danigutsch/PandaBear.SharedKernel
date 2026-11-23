using System.Globalization;

namespace PandaBear.Processes.Tests;

/// <summary>Reads the process ID that a test script wrote to a file with <c>printf '%s' "$$"</c>.</summary>
internal static class ProcessIdFile
{
    /// <summary>Returns the process ID in the file at <paramref name="path"/>.</summary>
    public static int Read(string path) => int.Parse(File.ReadAllText(path), CultureInfo.InvariantCulture);
}
