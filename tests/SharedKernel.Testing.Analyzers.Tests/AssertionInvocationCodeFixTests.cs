extern alias testingcodefixes;

using Microsoft.CodeAnalysis;

namespace PandaBear.Testing.Analyzers.Tests;

public sealed class AssertionInvocationCodeFixTests
{
    [Theory]
    [InlineData("var value = Enumerable.Single(values);", "Enumerable.Single(values)", "SKTEST012", "values.ShouldHaveSingleItem()")]
    [InlineData("var value = values.Single();", "values.Single()", "SKTEST012", "values.ShouldHaveSingleItem()")]
    [InlineData("var value = values.Single(item => item > 0);", "values.Single(item => item > 0)", "SKTEST012", "values.ShouldHaveSingleItem(item => item > 0)")]
    [InlineData("var value = values.Single(predicate);", "values.Single(predicate)", "SKTEST012", "ShouldHaveSingleItem")]
    [InlineData("var value = values.AsQueryable().Single(item => item > 0);", "values.AsQueryable().Single(item => item > 0)", "SKTEST012", "ShouldHaveSingleItem")]
    [InlineData("holder?.Text.ShouldBe(\"title\");", "holder?.Text.ShouldBe(\"title\")", "SKTEST014", "ShouldNotBeNull().ShouldBe(\"title\")")]
    [InlineData("holder?.Number.ShouldBe(1);", "holder?.Number.ShouldBe(1)", "SKTEST014", "ShouldNotBeNull().ShouldBe(1)")]
    [InlineData("holder?.Text?.ShouldBe(\"title\");", "holder?.Text?.ShouldBe(\"title\")", "SKTEST014", "ShouldNotBeNull().ShouldBe(\"title\")")]
    [InlineData("text?.ShouldBe(\"title\");", "text?.ShouldBe(\"title\")", "SKTEST014", "ShouldNotBeNull().ShouldBe(\"title\")")]
    public async Task Fix_preserves_compilation_and_removes_the_unsafe_diagnostic(string body, string marker, string diagnosticId, string expected)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(body);
        var source = AssertionDiagnosticTestSource.Create(body);
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(diagnosticId, marker);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var offeredActions = await workspace.GetCodeActions(provider, diagnostic);
        var action = offeredActions.ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(action);
        var updated = await workspace.GetDocumentText();
        var compilation = AnalyzerTestHarness.CreateCompilation(updated);
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(updated);

        // Assert
        updated.ShouldContain(expected, StringComparison.Ordinal);
        compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        diagnostics.ShouldNotContain(d => d.Id == diagnosticId || d.Id == "SKTEST013");
    }
    [Theory]
    [InlineData("holder?.Text.ShouldBe(\"title\")")]
    [InlineData("holder?.Number.ShouldBe(1)")]
    public async Task Fixed_conditional_assertions_fail_when_the_projected_receiver_is_null(string assertion)
    {
        // Arrange
        var source = AssertionDiagnosticTestSource.Create($"holder = null; {assertion};");
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic("SKTEST014", assertion);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var updated = await workspace.GetDocumentText();
        var compilation = AnalyzerTestHarness.CreateCompilation(updated, assemblyName: "NullAssertionProof_" + Guid.NewGuid().ToString("N"));
        using var image = new MemoryStream();
        compilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success.ShouldBeTrue();
        var assembly = System.Reflection.Assembly.Load(image.ToArray());
        var type = assembly.GetType("SampleTests").ShouldNotBeNull();
        var instance = Activator.CreateInstance(type);
        var method = type.GetMethod("Checks_behavior").ShouldNotBeNull();

        // Act
        Func<object?> invoke = () => method.Invoke(instance, null);
        var exception = invoke.ShouldThrow<System.Reflection.TargetInvocationException>();

        // Assert
        exception.InnerException.ShouldNotBeNull().GetType().Name.ShouldBe("NotNullException");
    }

    [Fact]
    public async Task Incompatible_single_replacements_are_not_offered()
    {
        // Arrange
        var source = AssertionDiagnosticTestSource.Create("var value = values.Single();") + """
            public static class OtherExtensions
            {
                public static int ShouldHaveSingleItem(this int[] values) => 0;
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic("SKTEST012", "values.Single()");
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("holder?.Text.ShouldBeNull()")]
    [InlineData("holder?.Text.ShouldBe((string?)null)")]
    public async Task Null_expectations_require_an_explicit_manual_receiver_guard(string assertion)
    {
        // Arrange
        var workspace = CodeFixTestWorkspace.Create(AssertionDiagnosticTestSource.Create(assertion + ";"));
        var diagnostic = await workspace.CreateDocumentDiagnostic("SKTEST014", assertion);
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }

}
