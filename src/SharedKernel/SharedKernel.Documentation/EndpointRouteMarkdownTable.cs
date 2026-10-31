using System.Text;
using System.Text.RegularExpressions;

namespace PandaBear.Documentation;

/// <summary>
/// Builds Markdown endpoint inventories from simple Minimal API route declarations.
/// </summary>
internal static partial class EndpointRouteMarkdownTable
{
    private const int MaximumEndpointChainLineCount = 128;
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    /// <summary>
    /// Builds an endpoint inventory table from C# source files.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="sourcePath"/> is <see langword="null"/>, empty, or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
    public static string Build(string sourcePath, DocumentationGeneratorBlock configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(configuration);

        var rows = SourceFiles(sourcePath)
            .SelectMany(file => RoutesFromFile(sourcePath, file, configuration))
            .OrderBy(route => route.Path, StringComparer.Ordinal)
            .ThenBy(route => route.Method, StringComparer.Ordinal)
            .ThenBy(route => route.Name, StringComparer.Ordinal)
            .Select(route => $"| `{route.Method}` | `{route.Path}` | {Escape(route.Name)} | {route.Audience} | {route.Auth} | `{route.Source}` |")
            .ToArray();

        return string.Join(
            '\n',
            [
                "| Method | Route | Endpoint | Audience | Auth metadata | Source |",
                "| --- | --- | --- | --- | --- | --- |",
                .. (rows.Length == 0 ? ["| n/a | n/a | No Minimal API routes discovered. | n/a | n/a | n/a |"] : rows),
            ]);
    }

    private static IEnumerable<(string Method, string Path, string Name, string Audience, string Auth, string Source)> RoutesFromFile(
        string sourcePath,
        FileInfo file,
        DocumentationGeneratorBlock configuration)
    {
        var lines = File.ReadAllLines(file.FullName, Utf8NoBom);
        var routePrefixes = new Dictionary<string, string>(configuration.RoutePrefixes, StringComparer.Ordinal);
        AddInlineRouteGroups(lines, routePrefixes);

        for (var index = 0; index < lines.Length; index++)
        {
            var match = EndpointMapRegex().Match(lines[index]);
            if (!match.Success)
            {
                continue;
            }

            var receiver = match.Groups[1].Value;
            var method = match.Groups[2].Value.ToUpperInvariant();
            var path = FullPath(receiver, match.Groups[3].Value, routePrefixes);
            var chain = EndpointChain(lines, index);
            var handler = match.Groups[4].Value.Trim();
            var source = Path.GetRelativePath(sourcePath, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
            yield return (method, path, EndpointName(chain, handler, configuration.EndpointNameMethods), Audience(path, source, configuration), AuthMetadata(chain), source);
        }
    }

    private static IEnumerable<FileInfo> SourceFiles(string sourcePath) =>
        new DirectoryInfo(sourcePath)
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(static file => !file.FullName.Split(Path.DirectorySeparatorChar).Contains("bin", StringComparer.Ordinal))
            .Where(static file => !file.FullName.Split(Path.DirectorySeparatorChar).Contains("obj", StringComparer.Ordinal))
            .OrderBy(file => file.FullName, StringComparer.Ordinal);

    private static void AddInlineRouteGroups(string[] lines, Dictionary<string, string> routePrefixes)
    {
        foreach (var line in lines)
        {
            var match = RouteGroupRegex().Match(line);
            if (match.Success)
            {
                routePrefixes[match.Groups[1].Value] = match.Groups[2].Value;
            }
        }
    }

    private static string FullPath(string receiver, string route, Dictionary<string, string> routePrefixes)
    {
        var prefix = routePrefixes.TryGetValue(receiver, out var configuredPrefix) ? configuredPrefix : string.Empty;
        if (prefix.Length == 0)
        {
            return NormalizePath(route);
        }

        var suffix = route == "/" ? string.Empty : route.TrimStart('/');
        var combined = suffix.Length == 0 ? prefix : $"{prefix.TrimEnd('/')}/{suffix}";
        return TrimTrailingSlash(NormalizePath(combined));
    }

    private static string NormalizePath(string path) => path.StartsWith('/') ? path : $"/{path}";

    private static string TrimTrailingSlash(string path) => path.Length > 1 ? path.TrimEnd('/') : path;

    private static string EndpointChain(string[] lines, int startIndex)
    {
        var builder = new StringBuilder();
        var endpointIndentation = Indentation(lines[startIndex]);
        for (var index = startIndex;
             index < lines.Length && index < startIndex + MaximumEndpointChainLineCount;
             index++)
        {
            if (index > startIndex
                && (EndpointMapStartRegex().IsMatch(lines[index])
                    || (Indentation(lines[index]) < endpointIndentation
                        && MemberDeclarationStartRegex().IsMatch(lines[index]))))
            {
                break;
            }

            builder.Append(lines[index]);
        }

        return builder.ToString();
    }

    private static int Indentation(string line)
    {
        var length = 0;
        while (length < line.Length && char.IsWhiteSpace(line[length]))
        {
            length++;
        }

        return length;
    }

    private static string EndpointName(string chain, string handler, IEnumerable<string> methods)
    {
        foreach (var method in methods)
        {
            var match = Regex.Match(chain, @"\." + Regex.Escape(method) + @"\(""([^""]+)""",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return handler.Contains("=>", StringComparison.Ordinal) ? "inline" : handler;
    }

    private static string Audience(string path, string source, DocumentationGeneratorBlock configuration)
    {
        foreach (var audience in configuration.SourceAudiences)
        {
            if (source.StartsWith(audience.Key, StringComparison.Ordinal))
            {
                return audience.Value;
            }
        }

        foreach (var audience in configuration.RouteAudiences)
        {
            if (Regex.IsMatch(path, audience.Key, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            {
                return audience.Value;
            }
        }

        return configuration.DefaultAudience;
    }

    private static string AuthMetadata(string chain)
    {
        if (chain.Contains(".RequireAuthorization", StringComparison.Ordinal))
        {
            return "required";
        }

        return chain.Contains(".AllowAnonymous", StringComparison.Ordinal) ? "anonymous" : "not declared";
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    [GeneratedRegex(@"^\s*var\s+(\w+)\s*=\s*app\.MapGroup\(""([^""]+)""\)", RegexOptions.CultureInvariant)]
    private static partial Regex RouteGroupRegex();

    [GeneratedRegex(@"^\s*(\w+)\.Map(Get|Post|Put|Delete|Patch)\(""([^""]+)""\s*,\s*([^;]+?)\)?\s*(?:;|$)", RegexOptions.CultureInvariant)]
    private static partial Regex EndpointMapRegex();

    [GeneratedRegex(@"^\s*\w+\.Map(?:Get|Post|Put|Delete|Patch)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex EndpointMapStartRegex();

    [GeneratedRegex(@"^\s*(?:public|private|protected|internal)\b", RegexOptions.CultureInvariant)]
    private static partial Regex MemberDeclarationStartRegex();

}
