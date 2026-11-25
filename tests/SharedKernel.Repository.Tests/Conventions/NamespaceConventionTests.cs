using System.Text.RegularExpressions;

namespace SharedKernel.Repository.Tests.Conventions;

/// <summary>
/// Guards the namespace decision in <c>docs/adr/20261124-sharedkernel-namespaces.md</c>: namespaces follow the project and assembly names,
/// <c>SharedKernel.*</c>, so no file under <c>src/</c> declares or names a <c>PandaBear</c> namespace. Package IDs keep the
/// <c>PandaBear.SharedKernel.*</c> prefix and stay allowed.
/// </summary>
public sealed partial class NamespaceConventionTests
{
    private static readonly HashSet<string> ScannedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".razor", ".csproj", ".props", ".targets", ".txt", ".md", ".proto", ".json",
    };

    [Fact]
    public void No_source_file_declares_or_names_a_pandabear_namespace()
    {
        // Arrange
        var root = Path.Combine(PackagingConventionTestFiles.GetRepositoryRoot(), "src");

        // Act
        var violations = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(static file => ScannedExtensions.Contains(Path.GetExtension(file))
                && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => File.ReadLines(file)
                .Select((line, index) => (Line: line, Number: index + 1))
                .Where(static entry => PandaBearNamespaceRegex().IsMatch(entry.Line))
                .Select(entry => $"{Path.GetRelativePath(root, file)}:{entry.Number}: {entry.Line.Trim()}"))
            .ToList();

        // Assert
        violations.ShouldBeEmpty();
    }

    [GeneratedRegex("""\bnamespace\s+PandaBear\b|\bglobal::PandaBear\b|\busing\s+(?:static\s+)?PandaBear\b|"PandaBear"|\bPandaBear\.(?!SharedKernel\b)[A-Za-z_]""")]
    private static partial Regex PandaBearNamespaceRegex();
}
