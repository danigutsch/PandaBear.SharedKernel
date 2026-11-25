using System.Globalization;
using Microsoft.CodeAnalysis;
using PandaBear.AspNetCore.CodeFixes;
using PandaBear.Testing.Roslyn;

namespace PandaBear.AspNetCore.Analyzers.Tests;

/// <summary>Exercises runtime mapping diagnostics and fixes against ASP.NET Core framework symbols.</summary>
public sealed class RuntimeEndpointBindingTests
{
    private const string Source = """
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Http;
        using Microsoft.AspNetCore.Routing;
        using System.Diagnostics.CodeAnalysis;

        namespace Demo;

        internal static class Routes
        {
            // Mapping comment.
            /*attributes*/
            internal static void Register(IEndpointRouteBuilder routes, Delegate handler)
            {
                routes.MapGet("/one", handler);
                routes.MapPost("/two", handler);
            }
        }
        """;

    [Theory]
    [InlineData("", true, true)]
    [InlineData("[RequiresDynamicCode(\"Reviewed\")]", false, true)]
    [InlineData("[RequiresUnreferencedCode(\"Reviewed\")]", true, false)]
    public async Task Fix_adds_only_missing_runtime_annotations(string existing, bool needsDynamicCode, bool needsTrimming)
    {
        // Arrange
        var source = Source.Replace("/*attributes*/", existing, StringComparison.Ordinal);
        if (string.IsNullOrEmpty(existing))
        {
            source = source.Replace("using System.Diagnostics.CodeAnalysis;\n", string.Empty, StringComparison.Ordinal);
        }

        var workspace = CodeFixWorkspace.Create(source);
        var analyzer = new RuntimeEndpointBindingAnalyzer();
        var provider = new RuntimeEndpointBindingCodeFixProvider();
        var diagnostics = await workspace.GetAnalyzerDiagnostics(analyzer);
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var changed = await workspace.GetDocumentText();
        var remaining = await workspace.GetAnalyzerDiagnostics(analyzer);
        var errors = (await workspace.GetCompilationDiagnostics())
            .Where(static candidate => candidate.Severity == DiagnosticSeverity.Error).ToArray();

        // Assert
        diagnostic.Id.ShouldBe("SKASPNET001");
        message.Contains("RequiresDynamicCodeAttribute", StringComparison.Ordinal).ShouldBe(needsDynamicCode);
        message.Contains("RequiresUnreferencedCodeAttribute", StringComparison.Ordinal).ShouldBe(needsTrimming);
        changed.Contains("Maps a route handler that relies on runtime-generated code.", StringComparison.Ordinal).ShouldBe(needsDynamicCode);
        changed.Contains("Maps a route handler that requires unreferenced application code.", StringComparison.Ordinal).ShouldBe(needsTrimming);
        changed.ShouldContain("using System.Diagnostics.CodeAnalysis;", StringComparison.Ordinal);
        changed.ShouldContain("// Mapping comment.", StringComparison.Ordinal);
        remaining.ShouldBeEmpty();
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Framework_request_delegate_overload_needs_no_annotation()
    {
        // Arrange
        var source = Source.Replace("Delegate handler", "RequestDelegate handler", StringComparison.Ordinal);
        var workspace = CodeFixWorkspace.Create(source);
        var analyzer = new RuntimeEndpointBindingAnalyzer();

        // Act
        var diagnostics = await workspace.GetAnalyzerDiagnostics(analyzer);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_local_mapping_function_can_be_reviewed_independently()
    {
        // Arrange
        var source = Source.Replace(
            """routes.MapGet("/one", handler);""",
            """void Local() => routes.MapGet("/one", handler); Local();""",
            StringComparison.Ordinal).Replace("""routes.MapPost("/two", handler);""", string.Empty, StringComparison.Ordinal);
        var workspace = CodeFixWorkspace.Create(source);
        var analyzer = new RuntimeEndpointBindingAnalyzer();
        var diagnostics = await workspace.GetAnalyzerDiagnostics(analyzer);
        var diagnostic = diagnostics.ShouldHaveSingleItem();

        // Act
        var actions = await workspace.GetCodeActions(new RuntimeEndpointBindingCodeFixProvider(), diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var remaining = await workspace.GetAnalyzerDiagnostics(analyzer);

        // Assert
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'Local'", StringComparison.Ordinal);
        remaining.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_anonymous_callback_does_not_require_annotations_on_its_enclosing_named_method()
    {
        // Arrange
        var source = Source.Replace(
            """routes.MapGet("/one", handler);""",
            """Action callback = () => routes.MapGet("/one", handler); callback();""",
            StringComparison.Ordinal).Replace("""routes.MapPost("/two", handler);""", string.Empty, StringComparison.Ordinal);
        var workspace = CodeFixWorkspace.Create(source);
        var analyzer = new RuntimeEndpointBindingAnalyzer();

        // Act
        var diagnostics = await workspace.GetAnalyzerDiagnostics(analyzer);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Analyzer_initialization_requires_a_context()
    {
        // Arrange
        var analyzer = new RuntimeEndpointBindingAnalyzer();
        Action initialize = () => analyzer.Initialize(null!);

        // Act
        var failure = initialize.ShouldThrow<ArgumentNullException>();

        // Assert
        failure.ParamName.ShouldBe("context");
    }

    [Fact]
    public async Task The_fix_requires_a_named_mapping_declaration_and_annotation_details()
    {
        // Arrange
        var workspace = CodeFixWorkspace.Create(Source);
        var provider = new RuntimeEndpointBindingCodeFixProvider();
        var noMethod = await workspace.CreateDocumentDiagnostic("SKASPNET001", "using Microsoft.AspNetCore.Builder");
        var noDetails = await workspace.CreateDocumentDiagnostic("SKASPNET001", "Register");

        // Act
        var missingMethodActions = await workspace.GetCodeActions(provider, noMethod);
        var missingDetailsActions = await workspace.GetCodeActions(provider, noDetails);

        // Assert
        provider.FixableDiagnosticIds.ShouldHaveSingleItem().ShouldBe("SKASPNET001");
        provider.GetFixAllProvider().ShouldNotBeNull();
        missingMethodActions.ShouldBeEmpty();
        missingDetailsActions.ShouldBeEmpty();
    }
}
