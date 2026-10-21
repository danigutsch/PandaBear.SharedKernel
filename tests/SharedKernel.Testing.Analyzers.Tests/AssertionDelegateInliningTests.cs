extern alias testingcodefixes;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Testing.Analyzers.Tests;

public sealed class AssertionDelegateInliningTests
{
    private const string DiagnosticId = "SKTEST011";

    [Theory]
    [InlineData("Action", "() => GC.Collect()", "")]
    [InlineData("Func<object?>", "() => new object()", "")]
    [InlineData("Func<Task>", "() => Task.CompletedTask", "await ")]
    [InlineData("Func<Task>", "async () => { await Task.Yield(); throw new InvalidOperationException(); }", "await ")]
    [InlineData("Func<Task>", "() => Task.CompletedTask", "await ", "ShouldThrowAssignableTo")]
    public async Task One_use_delegate_reports_suggestion_and_fixes_to_same_overload(string type, string lambda, string awaitPrefix, string assertionMethod = "ShouldThrow")
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(awaitPrefix);
        var source = $$"""
            using PandaBear.Testing.Assertions;
            public sealed class SampleTests
            {
                [Fact]
                public async Task Checks_exception()
                {
                    // Keep assertion explanation.
                    {{type}} action = {{lambda}};
                    {{awaitPrefix}}action.{{assertionMethod}}<InvalidOperationException>();
                }
            }
            """;
        var original = AnalyzerTestHarness.CreateCompilation(source);
        original.GetDiagnostics(TestContext.Current.CancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var originalModel = original.GetSemanticModel(original.SyntaxTrees.Single());
        var originalSymbol = (await original.SyntaxTrees.Single().GetRootAsync(TestContext.Current.CancellationToken)).DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(node => originalModel.GetSymbolInfo(node, TestContext.Current.CancellationToken).Symbol)
            .OfType<IMethodSymbol>().Where(method => method.Name == assertionMethod).ShouldHaveSingleItem();

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);
        var diagnostic = diagnostics.Where(static d => d.Id == DiagnosticId).ShouldHaveSingleItem();
        var workspace = CodeFixTestWorkspace.Create(source);
        var documentDiagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, $"action.{assertionMethod}<InvalidOperationException>()");
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var action = (await workspace.GetCodeActions(provider, documentDiagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(action);
        var updated = await workspace.GetDocumentText();
        var compilation = AnalyzerTestHarness.CreateCompilation(updated);
        var updatedModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());
        var updatedRoot = await compilation.SyntaxTrees.Single().GetRootAsync(TestContext.Current.CancellationToken);
        var updatedSymbol = updatedRoot.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(node => updatedModel.GetSymbolInfo(node, TestContext.Current.CancellationToken).Symbol)
            .OfType<IMethodSymbol>().Where(method => method.Name == assertionMethod).ShouldHaveSingleItem();

        // Assert
        updatedSymbol.ToDisplayString().ShouldBe(originalSymbol.ReducedFrom.ShouldNotBeNull()
            .ConstructedFrom.Construct(originalSymbol.TypeArguments.ToArray()).ToDisplayString());
        updatedRoot.DescendantNodes().OfType<AwaitExpressionSyntax>()
            .Any(static node => !node.Ancestors().OfType<LambdaExpressionSyntax>().Any()).ShouldBe(awaitPrefix.Length > 0);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        updated.ShouldNotContain("action =", StringComparison.Ordinal);
        updated.ShouldContain("// Keep assertion explanation.", StringComparison.Ordinal);
        updated.ShouldContain($"ShouldAssertionExtensions.{assertionMethod}<InvalidOperationException>", StringComparison.Ordinal);
        compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        (await AnalyzerTestHarness.GetAnalyzerDiagnostics(updated)).ShouldNotContain(static d => d.Id == DiagnosticId);
    }

    [Theory]
    [InlineData("Action action = () => { };", "action.ShouldThrow<Exception>(); action();")]
    [InlineData("Action action = () => { };", "action = () => { }; action.ShouldThrow<Exception>();")]
    [InlineData("Action action = GC.Collect;", "action.ShouldThrow<Exception>();")]
    [InlineData("Action action = () => { };", "GC.Collect(); action.ShouldThrow<Exception>();")]
    [InlineData("Action action = () => { };", "action?.ShouldThrow<Exception>();")]
    [InlineData("Action action = () => { };", "action.ShouldThrow<Exception>();", false)]
    [InlineData("Action action = () => throw new Exception();", "action.ShouldThrow<Exception>();")]
    [InlineData("Action action = () => { throw new Exception(); };", "action.ShouldThrow<Exception>();")]
    [InlineData("Action action = (Action)(() => { });", "action.ShouldThrow<Exception>();")]
    [InlineData("Action action = () => { };", "Action check = () => action.ShouldThrow<Exception>();")]
    [InlineData("Action /* retain declaration comment */ action = () => { };", "action.ShouldThrow<Exception>();")]
    [InlineData("Func<string> action = () => string.Empty;", "action.ShouldThrow<Exception>();")]
    [InlineData("Action action = () => { }, other = () => { };", "action.ShouldThrow<Exception>();")]
    [InlineData("Action action = () => { };", "GC.KeepAlive(action); action.ShouldThrow<Exception>();")]
    [InlineData("Action action = () => { };", "using var resource = new MemoryStream(); action.ShouldThrow<Exception>();")]
    public async Task Unsafe_or_unnecessary_rewrites_are_not_reported(string declaration, string assertion, bool isTest = true)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(assertion);
        var source = $$"""
            using PandaBear.Testing.Assertions;
            public sealed class SampleTests
            {
                {{(isTest ? "[Fact]" : "")}}
                public void Checks_exception()
                {
                    {{declaration}}
                    {{assertion}}
                }
            }
            """;

        AnalyzerTestHarness.CreateCompilation(source).GetDiagnostics(TestContext.Current.CancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);
        var workspace = CodeFixTestWorkspace.Create(source);
        var marker = assertion.Contains("action?", StringComparison.Ordinal)
            ? "action?.ShouldThrow<Exception>()"
            : "action.ShouldThrow<Exception>()";
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, marker);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        diagnostics.ShouldNotContain(static d => d.Id == DiagnosticId);
        actions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Static_named_argument_preserves_parameter_name_and_lambda_comments()
    {
        // Arrange
        const string source = """
            using PandaBear.Testing.Assertions;
            public sealed class SampleTests
            {
                [Fact]
                public void Checks_exception()
                {
                    Action action = () => { /* keep */ GC.Collect(); };
                    ShouldAssertionExtensions.ShouldThrow<ArgumentException>(paramName: "value", action: action);
                }
            }
            """;
        AnalyzerTestHarness.CreateCompilation(source).GetDiagnostics(TestContext.Current.CancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "ShouldAssertionExtensions.ShouldThrow<ArgumentException>(paramName: \"value\", action: action)");
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        (await AnalyzerTestHarness.GetAnalyzerDiagnostics(source)).ShouldContain(static diagnostic => diagnostic.Id == DiagnosticId);
        var action = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(action);
        var updated = await workspace.GetDocumentText();

        // Assert
        updated.ShouldContain("paramName: \"value\"", StringComparison.Ordinal);
        updated.ShouldContain("/* keep */", StringComparison.Ordinal);
        AnalyzerTestHarness.CreateCompilation(updated).GetDiagnostics(TestContext.Current.CancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Generated.g.cs", false)]
    [InlineData("Test.cs", true)]
    public async Task Generated_code_and_explicit_suppression_do_not_report_suggestion(string path, bool suppress)
    {
        // Arrange
        const string source = """
            using PandaBear.Testing.Assertions;
            public sealed class SampleTests
            {
                [Fact]
                public void Checks_exception()
                {
                    Action action = () => GC.Collect();
                    action.ShouldThrow<Exception>();
                }
            }
            """;
        var compilation = AnalyzerTestHarness.CreateCompilation(source, path: path);
        if (suppress)
        {
            compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
                compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, ReportDiagnostic.Suppress)));
        }

        // Act
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelTestingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == DiagnosticId);
    }

    [Theory]
    [InlineData("Exception Verify(int value) => action.ShouldThrow<Exception>();")]
    [InlineData("var exception = 5 switch { int value => action.ShouldThrow<Exception>() };")]
    public async Task Nested_scopes_do_not_rebind_captured_member_to_parameter_or_pattern(string assertion)
    {
        // Arrange
        var source = $$"""
            using PandaBear.Testing.Assertions;
            public sealed class SampleTests
            {
                private static int value => 1;
                [Fact]
                public void Checks_exception()
                {
                    Action action = () => GC.KeepAlive(value);
                    {{assertion}}
                }
            }
            """;
        AnalyzerTestHarness.CreateCompilation(source).GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "action.ShouldThrow<Exception>()");
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == DiagnosticId);
        actions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("void", "Action action = () => GC.Collect();", "var exception = action.ShouldThrow<Exception>();")]
    [InlineData("Task", "Func<Task> action = () => Task.CompletedTask;", "return action.ShouldThrow<Exception>();")]
    public async Task Direct_local_initializer_and_return_remain_compilable_after_fix(string returnType, string declaration, string assertion)
    {
        // Arrange
        var source = $$"""
            using PandaBear.Testing.Assertions;
            public sealed class SampleTests
            {
                [Fact]
                public {{returnType}} Checks_exception()
                {
                    {{declaration}}
                    {{assertion}}
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "action.ShouldThrow<Exception>()");
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        (await AnalyzerTestHarness.GetAnalyzerDiagnostics(source)).ShouldContain(static candidate => candidate.Id == DiagnosticId);
        var action = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(action);
        var updated = await workspace.GetDocumentText();

        // Assert
        updated.ShouldNotContain("action =", StringComparison.Ordinal);
        AnalyzerTestHarness.CreateCompilation(updated).GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static candidate => candidate.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Action action = () => GC.Collect();", "action /* retain */ .ShouldThrow<Exception>();")]
    [InlineData("Action action = () => { GC.Collect(); } /* retain */;", "action.ShouldThrow<Exception>();")]
    [InlineData("Action action = () => { GC.Collect(); }\n// retain\n;", "action.ShouldThrow<Exception>();")]
    [InlineData("Action action = () => GC.Collect();", "action.ShouldThrow<Exception> /* retain */ ();")]
    [InlineData("Action action =\n// retain\n() => GC.Collect();", "action.ShouldThrow<Exception>();")]
    public async Task Comments_that_would_be_lost_prevent_suggestion_and_fix(string declaration, string assertion)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(assertion);
        var source = $$"""
            using PandaBear.Testing.Assertions;
            public sealed class SampleTests
            {
                [Fact]
                public void Checks_exception()
                {
                    {{declaration}}
                    {{assertion}}
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, assertion.TrimEnd(';'));
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == DiagnosticId);
        actions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Delegate action = (Action)(() => { });")]
    [InlineData("object action = (Action)(() => { });")]
    [InlineData("Func<ValueTask> action = () => ValueTask.CompletedTask;")]
    public async Task Unsupported_delegate_contracts_remain_compile_errors_without_a_fix(string declaration)
    {
        // Arrange
        var source = $$"""
            using PandaBear.Testing.Assertions;
            public sealed class SampleTests
            {
                [Fact]
                public void Checks_exception()
                {
                    {{declaration}}
                    action.ShouldThrow<Exception>();
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, "action.ShouldThrow<Exception>()");
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var compilerDiagnostics = AnalyzerTestHarness.CreateCompilation(source).GetDiagnostics(TestContext.Current.CancellationToken);
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        compilerDiagnostics.ShouldContain(static candidate => candidate.Severity == DiagnosticSeverity.Error);
        diagnostics.ShouldNotContain(static candidate => candidate.Id == DiagnosticId);
        actions.ShouldBeEmpty();
    }

}
