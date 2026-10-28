extern alias testingcodefixes;

using System.Collections.Immutable;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Tests that SKTEST009 treats TestScope as the canonical form of a literal Scope trait.</summary>
public sealed class TestScopeTraitLiteralTests
{
    private const string DiagnosticId = TestingDiagnosticIds.XunitTraitConstantUsage;
    private const string TypedReplacement = "global::PandaBear.Testing.TestScope(global::PandaBear.Testing.TestScope.UiIntegration)";

    [Fact]
    public async Task Literal_scope_trait_reports_the_typed_attribute_as_replacement()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [Trait("Scope", "ui-integration")]
            public sealed class TourTests
            {
                [Fact]
                public void Creates_a_tour()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Properties["Replacement"].ShouldBe(TypedReplacement);
        diagnostic.Properties["Kind"].ShouldBe("TestScope");
    }

    [Fact]
    public async Task Literal_scope_trait_with_a_value_outside_the_scope_set_keeps_the_constant_advice()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [Trait("Scope", "bogus")]
            public sealed class TourTests
            {
                [Fact]
                public void Creates_a_tour()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Properties.ContainsKey("Kind"));
    }

    [Fact]
    public async Task Code_fix_replaces_the_trait_with_the_typed_attribute()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [Trait("Scope", "ui-integration")]
            public sealed class TourTests
            {
                [Fact]
                public void Creates_a_tour()
                {
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add("Replacement", TypedReplacement)
            .Add("Kind", "TestScope");
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "Trait(\"Scope\", \"ui-integration\")", properties);

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var updatedText = await workspace.GetDocumentText();

        // Assert
        updatedText.ShouldContain("[global::PandaBear.Testing.TestScope(global::PandaBear.Testing.TestScope.UiIntegration)]", StringComparison.Ordinal);
        updatedText.ShouldNotContain("Trait", StringComparison.Ordinal);
    }
}
