namespace SharedKernel.Versioning.Tool;

internal static class VersionOutputJson
{
    public static string Serialize(VersionOutput output) =>
        "{" +
        $"\"semVer\":{Escape(output.SemVer)}," +
        $"\"releaseImpact\":{Escape(ReleaseImpactText.ToOutputValue(output.ReleaseImpact))}," +
        $"\"packageVersion\":{Escape(output.PackageVersion)}," +
        $"\"assemblyVersion\":{Escape(output.AssemblyVersion)}," +
        $"\"fileVersion\":{Escape(output.FileVersion)}," +
        $"\"informationalVersion\":{Escape(output.InformationalVersion)}" +
        "}";

    private static string Escape(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal) + "\"";
    }
}
