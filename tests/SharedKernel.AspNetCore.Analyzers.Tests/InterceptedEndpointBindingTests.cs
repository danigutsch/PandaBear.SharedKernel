using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PandaBear.Testing.Roslyn;

namespace PandaBear.AspNetCore.Analyzers.Tests;

/// <summary>Exercises the effective method selected by Roslyn end-to-end.</summary>
public sealed class InterceptedEndpointBindingTests
{
    [Fact]
    public async Task A_project_without_the_framework_route_builder_is_outside_the_rule()
    {
        // Arrange
        var syntax = CSharpSyntaxTree.ParseText("internal static class Plain { internal static void Register() { } }",
            cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create("Plain", [syntax],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var analyzer = new RuntimeEndpointBindingAnalyzer();

        // Act
        var diagnostics = await compilation.WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_intercepted_mapping_uses_the_generated_method_annotations()
    {
        // Arrange
        const string source = """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Routing;

            internal static class Routes
            {
                internal static void Register(IEndpointRouteBuilder routes) => routes.MapGet("/generated", static () => "OK");
            }
            """;
        var ct = TestContext.Current.CancellationToken;
        var parse = new CSharpParseOptions(LanguageVersion.Preview)
            .WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "Demo.Generated")]);
        var syntax = CSharpSyntaxTree.ParseText(source, parse, "/Routes.cs", cancellationToken: ct);
        var compilation = AnalyzerTestHarness.CreateCompilation(source, string.Empty).RemoveAllSyntaxTrees().AddSyntaxTrees(syntax);
        var root = await syntax.GetRootAsync(ct);
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().ShouldHaveSingleItem();
        var model = compilation.GetSemanticModel(syntax);
        var location = model.GetInterceptableLocation(invocation, ct).ShouldNotBeNull();
        var attribute = location.GetInterceptsLocationAttributeSyntax();
        var generated = $$"""
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Routing;

            namespace System.Runtime.CompilerServices
            {
                [global::System.AttributeUsage(global::System.AttributeTargets.Method)]
                file sealed class InterceptsLocationAttribute(int version, string data) : global::System.Attribute
                {
                    public int Version { get; } = version;
                    public string Data { get; } = data;
                }
            }

            namespace Demo.Generated
            {
                internal static class EndpointHooks
                {
                    {{attribute}}
                    internal static RouteHandlerBuilder Generated(this IEndpointRouteBuilder routes, string pattern, global::System.Delegate handler) => throw new global::System.NotImplementedException();
                }
            }
            """;
        var generatedTree = CSharpSyntaxTree.ParseText(generated, parse, "/Routes.g.cs", cancellationToken: ct);
        var intercepted = compilation.AddSyntaxTrees(generatedTree);
        var analyzer = new RuntimeEndpointBindingAnalyzer();

        // Act
        var fallback = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(ct);
        var generatedDiagnostics = await intercepted.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(ct);
        var compilerErrors = intercepted.GetDiagnostics(ct).Where(static item => item.Severity == DiagnosticSeverity.Error).ToArray();

        // Assert
        fallback.ShouldHaveSingleItem().Id.ShouldBe("SKASPNET001");
        generatedDiagnostics.ShouldBeEmpty();
        compilerErrors.ShouldBeEmpty();
    }
}
