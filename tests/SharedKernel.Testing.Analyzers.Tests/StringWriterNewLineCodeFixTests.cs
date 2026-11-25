extern alias testingcodefixes;

using Microsoft.CodeAnalysis;

namespace SharedKernel.Testing.Analyzers.Tests;

/// <summary>Tests the SKTEST022 code fix that replaces a platform line ending StringWriter with InvariantStringWriter.</summary>
public sealed class StringWriterNewLineCodeFixTests
{
    private const string DiagnosticId = TestingDiagnosticIds.StringWriterNewLine;

    [Theory]
    [InlineData("new StringWriter()", "new InvariantStringWriter()")]
    [InlineData("new StringWriter(CultureInfo.InvariantCulture)", "new InvariantStringWriter()")]
    [InlineData("new StringWriter(System.Globalization.CultureInfo.InvariantCulture)", "new InvariantStringWriter()")]
    public async Task A_string_writer_becomes_an_invariant_string_writer(string creation, string expected)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(creation);
        var source = StringWriterNewLineTestSource.Create(creation);
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, creation);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.TestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var updated = await workspace.GetDocumentText();
        var compilerErrors = AnalyzerTestHarness.CreateCompilation(updated).GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error);
        var analyzerDiagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(updated);

        // Assert
        updated.ShouldContain($"TextWriter writer = {expected};", StringComparison.Ordinal);
        updated.ShouldContain("using SharedKernel.Testing;", StringComparison.Ordinal);
        compilerErrors.ShouldBeEmpty();
        analyzerDiagnostics.ShouldNotContain(static d => d.Id == DiagnosticId);
    }

    [Fact]
    public async Task A_target_typed_string_writer_becomes_an_invariant_string_writer()
    {
        // Arrange
        const string source = """
            namespace Demo
            {
                public sealed class Capture
                {
                    public StringWriter Output { get; } = new();
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "new()");
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.TestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var updated = await workspace.GetDocumentText();

        // Assert
        updated.ShouldContain("public StringWriter Output { get; } = new InvariantStringWriter();", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("new StringWriter(builder)")]
    [InlineData("new StringWriter(builder, CultureInfo.InvariantCulture)")]
    [InlineData("new StringWriter(CultureInfo.CurrentCulture)")]
    public async Task A_writer_over_a_builder_or_with_another_culture_is_not_rewritten(string creation)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(creation);
        var source = StringWriterNewLineTestSource.Create(creation);
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, creation);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.TestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }
}
