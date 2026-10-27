extern alias testingcodefixes;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Tests how the SKTEST002 test-name rule treats canonical technical keywords such as API, HTTP2 and ValueTask.</summary>
public sealed class TestMethodKeywordTests
{
    [Theory]
    [InlineData("Checks_API_behavior", true)]
    [InlineData("API_checks_behavior", true)]
    [InlineData("Api_checks_behavior", false)]
    [InlineData("Checks_customapi_behavior", true)]
    [InlineData("Checks_Api_behavior", false)]
    [InlineData("Checks_api_behavior", false)]
    [InlineData("Checks_AsyncAPI_behavior", true)]
    [InlineData("Checks_AsyncApi_behavior", false)]
    [InlineData("Checks_CustomAPI_behavior", false)]
    [InlineData("Checks_HTTP2_behavior", true)]
    [InlineData("Checks_ValueTask_behavior", true)]
    [InlineData("Checks_HttpClient_behavior", true)]
    [InlineData("Checks_OpenApi_behavior", true)]
    [InlineData("Checks_ID_GUID_CSV_UI_xUnit_behavior", true)]
    [InlineData("Checks_SKTEST004_behavior", true)]
    public async Task Strict_names_enforce_canonical_keywords(string name, bool accepted)
    {
        // Arrange
        var source = $"public sealed class SampleTests {{ [Fact] public void {name}() {{ }} }}";

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.Any(static d => d.Id == "SKTEST002").ShouldBe(!accepted);
    }

    [Theory]
    [InlineData("ChecksApiBehavior", "Checks_API_behavior")]
    [InlineData("Checks_AsyncApi_behavior", "Checks_AsyncAPI_behavior")]
    [InlineData("Checks_SKTEST004_behavior", "Checks_SKTEST004_behavior")]
    [InlineData("ChecksHTTP2ValueTaskHttpClientOpenApiBehavior", "Checks_HTTP2_ValueTask_HttpClient_OpenApi_behavior")]
    public async Task Name_fix_preserves_canonical_keywords(string original, string expected)
    {
        // Arrange
        var source = $"public sealed class SampleTests {{ [Fact] public void {original}() {{ }} }}";
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic("SKTEST002", original);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        if (original != expected)
        {
            await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        }
        var updated = await workspace.GetDocumentText();

        // Assert
        updated.ShouldContain(expected, StringComparison.Ordinal);
        if (original == expected)
        {
            actions.ShouldBeEmpty();
        }
    }
}
