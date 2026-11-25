using System.Text.RegularExpressions;

namespace SharedKernel.Repository.Tests.Conventions;

/// <summary>Reads the string literals and named HTTP client constants of the SharedKernel sources for the runtime identifier guards.</summary>
internal static partial class RuntimeIdentifierSourceFiles
{
    private static string SourceRoot => Path.Combine(PackagingConventionTestFiles.GetRepositoryRoot(), "src", "SharedKernel");

    /// <summary>Gets every string literal in the SharedKernel sources.</summary>
    /// <returns>Each literal with the source file, relative to <c>src/SharedKernel</c>.</returns>
    internal static List<(string File, string Value)> Literals() =>
        [.. SourceFiles().SelectMany(static file => StringLiteralRegex().Matches(File.ReadAllText(file))
            .Select(match => (File: Path.GetRelativePath(SourceRoot, file), Value: match.Groups["value"].Value)))];

    /// <summary>Gets the values of the string constants whose names end in <c>HttpClientName</c>.</summary>
    /// <returns>Each named HTTP client with the source file, relative to <c>src/SharedKernel</c>.</returns>
    internal static List<(string File, string Name)> HttpClientNames() =>
        [.. SourceFiles().SelectMany(static file => HttpClientNameRegex().Matches(File.ReadAllText(file))
            .Select(match => (File: Path.GetRelativePath(SourceRoot, file), Name: match.Groups["name"].Value)))];

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(SourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    [GeneratedRegex(@"(?<!\\)""(?<value>(?:[^""\\\r\n]|\\.)*)""")]
    private static partial Regex StringLiteralRegex();

    [GeneratedRegex("""\bconst\s+string\s+\w*HttpClientName\s*=\s*"(?<name>[^"]+)"\s*;""")]
    private static partial Regex HttpClientNameRegex();
}
