using System.Runtime.CompilerServices;
using Xunit.Sdk;

namespace SharedKernel.Mediator.GeneratorTests;

/// <summary>
/// Verifies generated source against a committed approval snapshot.
/// </summary>
internal static class GeneratorSnapshotVerifier
{
    /// <summary>
    /// Compares generated output with the approved snapshot for the calling test.
    /// </summary>
    /// <param name="actual">The generated source text to verify.</param>
    /// <param name="filePath">The calling test file path.</param>
    /// <param name="testName">The calling test method name.</param>
    public static void Verify(
        string actual,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string testName = "")
    {
        var snapshotDirectory = Path.Combine(Path.GetDirectoryName(filePath)!, "Snapshots");
        Directory.CreateDirectory(snapshotDirectory);

        var snapshotName = $"{Path.GetFileNameWithoutExtension(filePath)}.{testName}";
        var verifiedPath = Path.Combine(snapshotDirectory, $"{snapshotName}.verified.cs");
        var receivedPath = Path.Combine(snapshotDirectory, $"{snapshotName}.received.cs");

        var normalizedActual = Normalize(actual);

        if (!File.Exists(verifiedPath))
        {
            File.WriteAllText(receivedPath, normalizedActual);
            throw new XunitException($"Missing snapshot: {verifiedPath}");
        }

        var normalizedExpected = Normalize(File.ReadAllText(verifiedPath));

        if (!string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal))
        {
            File.WriteAllText(receivedPath, normalizedActual);
            throw new XunitException($"Snapshot mismatch: {verifiedPath}");
        }

        if (File.Exists(receivedPath))
        {
            File.Delete(receivedPath);
        }
    }

    /// <summary>
    /// Normalizes line endings and trailing whitespace for stable snapshot comparisons.
    /// </summary>
    /// <param name="content">The content to normalize.</param>
    /// <returns>The normalized content.</returns>
    private static string Normalize(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
    }
}
