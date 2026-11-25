namespace SharedKernel.Versioning.Tests;

/// <summary>Writes a root <c>Directory.Build.props</c> with a single property group for framework-reading tests.</summary>
internal static class FrameworkPropsFile
{
    /// <summary>Writes <c>Directory.Build.props</c> in <paramref name="root"/> with <paramref name="properties"/> in one property group.</summary>
    public static void Write(string root, string properties) =>
        File.WriteAllText(Path.Combine(root, "Directory.Build.props"), "<Project><PropertyGroup>" + properties + "</PropertyGroup></Project>");
}
