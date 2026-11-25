namespace SharedKernel.Secrets.Sops.Tests;

/// <summary>Picks lines out of an encrypted SOPS document.</summary>
internal static class SopsDocumentLines
{
    /// <summary>Returns the line that holds the MAC, which changes with the data key.</summary>
    public static string Mac(string document) =>
        document.Split('\n').Single(static line => line.Contains("\"mac\"", StringComparison.Ordinal) || line.TrimStart().StartsWith("mac:", StringComparison.Ordinal));
}
