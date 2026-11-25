namespace SharedKernel.ConfigurationFiles.Tests;

/// <summary>Writes <c>.config/sample.json</c> under a temporary root.</summary>
internal static class SampleFile
{
    /// <summary>Writes <paramref name="json"/> as the sample configuration file.</summary>
    public static async Task Write(TemporaryDirectory root, string json)
    {
        Directory.CreateDirectory(Path.Combine(root.Path, ".config"));
        await File.WriteAllTextAsync(Path.Combine(root.Path, ".config", "sample.json"), json, TestContext.Current.CancellationToken);
    }
}
