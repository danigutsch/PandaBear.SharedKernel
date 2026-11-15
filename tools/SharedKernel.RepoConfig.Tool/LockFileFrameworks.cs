using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Reads the target frameworks that a <c>packages.lock.json</c> file locks.
/// </summary>
internal static partial class LockFileFrameworks
{
    /// <summary>
    /// Returns the normalized, distinct frameworks named by the keys of the top-level <c>dependencies</c> object, or
    /// <see langword="null"/> when the content is not a lock file.
    /// </summary>
    public static IReadOnlyList<string>? Read(ReadOnlyMemory<byte> content)
    {
        try
        {
            using var document = JsonDocument.Parse(content, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("dependencies", out var dependencies)
                || dependencies.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return dependencies.EnumerateObject().Select(static property => Normalize(property.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Turns a lock file key into a target framework moniker, lowercase for the long forms,: the text before a <c>/</c> runtime identifier
    /// is kept, <c>.NETStandard,Version=vX.Y</c> becomes <c>netstandardX.Y</c>, <c>.NETCoreApp,Version=vX.Y</c> becomes
    /// <c>netX.Y</c> from version 5 and <c>netcoreappX.Y</c> before it, and <c>.NETFramework,Version=vA.B[.C]</c> becomes
    /// <c>netABC</c>.
    /// </summary>
    public static string Normalize(string key)
    {
        var slash = key.IndexOf('/', StringComparison.Ordinal);
        var framework = (slash < 0 ? key : key[..slash]).Trim();
        var match = Long().Match(framework);
        if (!match.Success)
        {
            return framework;
        }

        var version = match.Groups["version"].Value;
        return match.Groups["name"].Value.ToUpperInvariant() switch
        {
            ".NETSTANDARD" => "netstandard" + version,
            ".NETCOREAPP" => (int.Parse(version.Split('.')[0], CultureInfo.InvariantCulture) >= 5 ? "net" : "netcoreapp") + version,
            _ => "net" + version.Replace(".", string.Empty, StringComparison.Ordinal),
        };
    }

    [GeneratedRegex(@"^(?<name>\.NET(?:Standard|CoreApp|Framework)),Version=v(?<version>\d+(?:\.\d+)*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Long();
}
