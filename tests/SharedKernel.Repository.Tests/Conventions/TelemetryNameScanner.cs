using System.Text.RegularExpressions;

namespace PandaBear.Repository.Tests.Conventions;

/// <summary>
/// Reads the telemetry names that libraries declare: <c>MeterName</c> and <c>ActivitySourceName</c> constants anywhere, and the
/// <c>Name</c> member of a type whose file name ends in <c>Telemetry</c>.
/// </summary>
internal static partial class TelemetryNameScanner
{
    /// <summary>Finds the meter and activity source names declared in library source.</summary>
    /// <param name="sourceRoot">The root of the library sources.</param>
    /// <returns>Each name with the file that declares it.</returns>
    internal static List<(string File, string Name)> DeclaredTelemetryNames(string sourceRoot)
    {
        List<(string File, string Name)> names = [];
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            if (relative.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || relative.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || relative.StartsWith("SharedKernel.Testing", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            var isTelemetryType = Path.GetFileNameWithoutExtension(file).EndsWith("Telemetry", StringComparison.Ordinal);
            foreach (Match match in DeclaredNameRegex().Matches(text))
            {
                names.Add((relative, match.Groups["name"].Value));
            }

            if (isTelemetryType)
            {
                foreach (Match match in TelemetryTypeNameRegex().Matches(text))
                {
                    names.Add((relative, match.Groups["name"].Value));
                }
            }
        }

        return names;
    }

    [GeneratedRegex("""(?:MeterName|ActivitySourceName)\s*(?:=|=>)\s*"(?<name>[^"]+)"\s*;""")]
    private static partial Regex DeclaredNameRegex();

    [GeneratedRegex("""public\s+(?:const\s+string|static\s+string)\s+Name\s*(?:=|=>)\s*"(?<name>[^"]+)"\s*;""")]
    private static partial Regex TelemetryTypeNameRegex();
}
