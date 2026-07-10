namespace SharedKernel.Versioning;

/// <summary>
/// Serializes release version output to the stable JSON contract used by automation.
/// </summary>
public static class VersionOutputJson
{
    /// <summary>
    /// Serializes version output to compact JSON.
    /// </summary>
    /// <param name="output">The version output.</param>
    /// <returns>The JSON representation.</returns>
    public static string Serialize(VersionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        return "{" +
            $"\"semVer\":{Escape(output.SemVer)}," +
            $"\"releaseImpact\":{Escape(ReleaseImpactText.ToOutputValue(output.ReleaseImpact))}," +
            $"\"packageVersion\":{Escape(output.PackageVersion)}," +
            $"\"assemblyVersion\":{Escape(output.AssemblyVersion)}," +
            $"\"fileVersion\":{Escape(output.FileVersion)}," +
            $"\"informationalVersion\":{Escape(output.InformationalVersion)}" +
            "}";
    }

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
