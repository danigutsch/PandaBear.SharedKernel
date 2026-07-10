using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using SharedKernel.Testing.Roslyn;

namespace SharedKernel.Testing.CodeFixRunner.Tests;

public sealed class DiagnosticLocationKeyTests
{
    [Fact]
    public void Create_returns_path_line_and_character()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "sample.cs");
        var sourceText = SourceText.From("first\nsecond\nthird", Encoding.UTF8);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            sourceText,
            path: path,
            cancellationToken: TestContext.Current.CancellationToken);
        var descriptor = new DiagnosticDescriptor(
            "SKTEST999",
            "Title",
            "Message",
            "Testing",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(
            descriptor,
            Location.Create(syntaxTree, TextSpan.FromBounds(8, 14)));

        // Act
        var key = DiagnosticLocationKey.Create(diagnostic);

        // Assert
        key.ShouldBe($"{path}:1:2");
    }
}
