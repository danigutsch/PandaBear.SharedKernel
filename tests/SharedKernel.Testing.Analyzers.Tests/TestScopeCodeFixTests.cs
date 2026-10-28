extern alias testingcodefixes;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Tests the code actions offered for SKTEST018.</summary>
public sealed class TestScopeCodeFixTests
{
    private const string DiagnosticId = TestingDiagnosticIds.XunitTestScope;
    private const string AllScopes = "unit;behavior;component;contract;ui-integration;integration;system;architecture";

    private const string Source = """
        namespace Demo;

        public sealed class TourTests
        {
            [Fact]
            public void Creates_a_tour()
            {
            }
        }
        """;

    [Fact]
    public async Task Missing_scope_offers_one_typed_action_per_allowed_scope()
    {
        // Arrange
        var workspace = CodeFixTestWorkspace.Create(Source);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "Creates_a_tour()", TestScopeDiagnosticProperties.Create("missing", AllScopes, typed: true));

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        actions.Select(static action => action.Title).ShouldBe(
        [
            "Add TestScope(TestScope.Unit)",
            "Add TestScope(TestScope.Behavior)",
            "Add TestScope(TestScope.Component)",
            "Add TestScope(TestScope.Contract)",
            "Add TestScope(TestScope.UiIntegration)",
            "Add TestScope(TestScope.Integration)",
            "Add TestScope(TestScope.System)",
            "Add TestScope(TestScope.Architecture)",
        ]);
    }

    [Fact]
    public async Task Typed_action_adds_the_attribute_to_the_class()
    {
        // Arrange
        var workspace = CodeFixTestWorkspace.Create(Source);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "Creates_a_tour()", TestScopeDiagnosticProperties.Create("missing", AllScopes, typed: true));
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Act
        await workspace.ApplyCodeAction(actions.First(static action => action.Title == "Add TestScope(TestScope.UiIntegration)"));
        var updatedText = await workspace.GetDocumentText();

        // Assert
        const string attribute = "[global::PandaBear.Testing.TestScope(global::PandaBear.Testing.TestScope.UiIntegration)]";
        updatedText.IndexOf(attribute, StringComparison.Ordinal).ShouldBeGreaterThan(-1);
        updatedText.IndexOf(attribute, StringComparison.Ordinal).ShouldBeLessThan(updatedText.IndexOf("class TourTests", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Trait_action_is_offered_when_the_typed_attribute_is_not_referenced()
    {
        // Arrange
        var workspace = CodeFixTestWorkspace.Create(Source);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "Creates_a_tour()", TestScopeDiagnosticProperties.Create("missing", "unit;integration", typed: false));
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Act
        await workspace.ApplyCodeAction(actions.First(static action => action.Title == "Add Trait(\"Scope\", \"integration\")"));
        var updatedText = await workspace.GetDocumentText();

        // Assert
        actions.Count.ShouldBe(2);
        updatedText.ShouldContain("[global::Xunit.Trait(\"Scope\", \"integration\")]", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("conflicting")]
    public async Task Unknown_or_conflicting_scope_offers_no_action(string reason)
    {
        // Arrange
        var workspace = CodeFixTestWorkspace.Create(Source);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "Creates_a_tour()", TestScopeDiagnosticProperties.Create(reason, AllScopes, typed: true));

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Nested_test_class_receives_the_attribute_on_its_own_declaration()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Outer
            {
                public sealed class Inner
                {
                    [Fact]
                    public void Creates_a_tour()
                    {
                    }
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "Creates_a_tour()", TestScopeDiagnosticProperties.Create("missing", "unit", typed: true));
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Act
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var updatedText = await workspace.GetDocumentText();

        // Assert
        const string attribute = "[global::PandaBear.Testing.TestScope(global::PandaBear.Testing.TestScope.Unit)]";
        updatedText.IndexOf(attribute, StringComparison.Ordinal).ShouldBeGreaterThan(updatedText.IndexOf("class Outer", StringComparison.Ordinal));
        updatedText.IndexOf(attribute, StringComparison.Ordinal).ShouldBeLessThan(updatedText.IndexOf("class Inner", StringComparison.Ordinal));
    }
}
