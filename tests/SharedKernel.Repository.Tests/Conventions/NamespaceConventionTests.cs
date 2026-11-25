using System.Text.RegularExpressions;

namespace SharedKernel.Repository.Tests.Conventions;

/// <summary>
/// Guards the namespace decision in <c>docs/adr/20261124-sharedkernel-namespaces.md</c>: namespaces follow the project and assembly names,
/// <c>SharedKernel.*</c>, so no file under <c>src/</c> declares or names a <c>PandaBear</c> namespace. Package IDs keep the
/// <c>PandaBear.SharedKernel.*</c> prefix. The exact persisted BFF protection purposes remain compatible with earlier versions.
/// </summary>
public sealed partial class NamespaceConventionTests
{
    private static readonly HashSet<string> ScannedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".razor", ".csproj", ".props", ".targets", ".txt", ".md", ".proto", ".json",
    };

    private static readonly HashSet<(string Path, string Declaration)> PersistedBffPurposes =
    [
        ("SharedKernel/SharedKernel.AspNetCore.Authentication.Bff/ServerSideTicketStore.cs",
            """private const string ProtectorPurpose = "PandaBear.AspNetCore.Authentication.Bff.ServerSideTicketStore.v1";"""),
        ("SharedKernel/SharedKernel.AspNetCore.Authentication.Bff/ProtectedTokenCache.cs",
            """private const string UserTokensPurpose = "PandaBear.AspNetCore.Authentication.Bff.UserTokens.v1";"""),
        ("SharedKernel/SharedKernel.AspNetCore.Authentication.Bff/ProtectedTokenCache.cs",
            """private const string ExchangedTokenPurpose = "PandaBear.AspNetCore.Authentication.Bff.ExchangedToken.v1";"""),
    ];

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
                .Where(entry => PandaBearNamespaceRegex().IsMatch(entry.Line)
                    && !PersistedBffPurposes.Contains((Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'), entry.Line.Trim())))
                .Select(entry => $"{Path.GetRelativePath(root, file)}:{entry.Number}: {entry.Line.Trim()}"))
            .ToList();

        // Assert
        violations.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("SharedKernel/SharedKernel.AspNetCore.Authentication.Bff/ProtectedTokenCache.cs",
        """private const string UserTokensPurpose = "PandaBear.AspNetCore.Authentication.Bff.UserTokens.v1";""", true)]
    [InlineData("SharedKernel/SharedKernel.AspNetCore.Authentication.Bff/AnotherStore.cs",
        """private const string UserTokensPurpose = "PandaBear.AspNetCore.Authentication.Bff.UserTokens.v1";""", false)]
    [InlineData("SharedKernel/SharedKernel.AspNetCore.Authentication.Bff/ProtectedTokenCache.cs",
        "using PandaBear.AspNetCore.Authentication.Bff;", false)]
    public void Persisted_purpose_exemption_is_limited_to_the_exact_declaration(string path, string line, bool expected)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(line);

        // Arrange
        var namespacePattern = PandaBearNamespaceRegex();

        // Act
        var allowedPurpose = PersistedBffPurposes.Contains((path, line));

        // Assert
        namespacePattern.IsMatch(line).ShouldBeTrue();
        allowedPurpose.ShouldBe(expected);
    }

    [GeneratedRegex("""\bnamespace\s+PandaBear\b|\bglobal::PandaBear\b|\busing\s+(?:static\s+)?PandaBear\b|"PandaBear"|\bPandaBear\.(?!SharedKernel\b)[A-Za-z_]""")]
    private static partial Regex PandaBearNamespaceRegex();
}
