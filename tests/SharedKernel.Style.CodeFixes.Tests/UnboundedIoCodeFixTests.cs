namespace PandaBear.Style.CodeFixes.Tests;

public sealed class UnboundedIoCodeFixTests
{
    private const string DiagnosticId = "SKSTYLE011";

    [Theory]
    [InlineData("reader.ReadToEnd()")]
    [InlineData("reader.ReadToEndAsync(ct)")]
    [InlineData("File.ReadAllText(path)")]
    [InlineData("File.ReadAllTextAsync(path, ct)")]
    [InlineData("File.ReadAllBytes(path)")]
    [InlineData("File.ReadAllBytesAsync(path, ct)")]
    public async Task Whole_read_diagnostic_does_not_invent_limits_or_offer_an_unsafe_rewrite(string expression)
    {
        // Arrange
        var source = $$"""
            public sealed class Importer
            {
                public object Execute(string path, StreamReader reader, CancellationToken ct) => {{expression}};
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, expression);
        var provider = new SharedKernelStyleCodeFixProvider();

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);
        var bulkDiagnostics = provider.GetFixAllProvider().GetSupportedFixAllDiagnosticIds(provider);

        // Assert
        provider.FixableDiagnosticIds.ShouldNotContain(DiagnosticId);
        codeActions.ShouldBeEmpty();
        bulkDiagnostics.ShouldNotContain(DiagnosticId);
    }
}
