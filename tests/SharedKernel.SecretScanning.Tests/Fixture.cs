namespace PandaBear.SecretScanning.Tests;

/// <summary>Loads the sample files that are copied next to the test assembly.</summary>
internal static class Fixture
{
    /// <summary>Returns the text of a file below the output Fixtures folder.</summary>
    /// <param name="relativePath">The path relative to the Fixtures folder.</param>
    public static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath));
}
