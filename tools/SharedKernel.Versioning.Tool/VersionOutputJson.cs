using System.Text.Json;

namespace SharedKernel.Versioning.Tool;

internal static class VersionOutputJson
{
    public static string Serialize(VersionOutput output) =>
        "{" +
        $"\"semVer\":{JsonSerializer.Serialize(output.SemVer)}," +
        $"\"releaseImpact\":{JsonSerializer.Serialize(ReleaseImpactText.ToOutputValue(output.ReleaseImpact))}," +
        $"\"packageVersion\":{JsonSerializer.Serialize(output.PackageVersion)}," +
        $"\"assemblyVersion\":{JsonSerializer.Serialize(output.AssemblyVersion)}," +
        $"\"fileVersion\":{JsonSerializer.Serialize(output.FileVersion)}," +
        $"\"informationalVersion\":{JsonSerializer.Serialize(output.InformationalVersion)}" +
        "}";
}
