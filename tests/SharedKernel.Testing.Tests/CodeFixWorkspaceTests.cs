using Microsoft.CodeAnalysis;
using PandaBear.Testing.Roslyn;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the in-memory workspace for analyzer and code fix tests.</summary>
public sealed class CodeFixWorkspaceTests
{
    [Fact]
    public async Task A_code_fix_changes_the_document_at_the_diagnostic_marker()
    {
        // Arrange
        var workspace = CodeFixWorkspace.Create("public sealed class Broken { }");
        var diagnostic = await workspace.CreateDocumentDiagnostic(ReplacingCodeFix.Id, "Broken");
        var actions = await workspace.GetCodeActions(new ReplacingCodeFix(), diagnostic);
        var action = actions.ShouldHaveSingleItem();

        // Act
        await workspace.ApplyCodeAction(action);

        // Assert
        var text = await workspace.GetDocumentText();
        var diagnostics = await workspace.GetCompilationDiagnostics();
        text.ShouldBe(CodeFixWorkspaceOptions.CommonUsings + "public sealed class Fixed { }");
        diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact]
    public async Task Added_documents_get_the_default_usings_and_can_carry_the_diagnostic()
    {
        // Arrange
        var workspace = CodeFixWorkspace.Create("public sealed class Main { }", new CodeFixWorkspaceOptions { DefaultUsings = "using System;\n" });
        var other = workspace.AddDocument("public sealed class Other { }", "Other.cs");
        var diagnostic = await workspace.CreateDocumentDiagnostic(ReplacingCodeFix.Id, "Other.cs", "Other");
        var actions = await workspace.GetCodeActions(new ReplacingCodeFix(), diagnostic);
        var action = actions.ShouldHaveSingleItem();

        // Act
        await workspace.ApplyCodeAction(action);

        // Assert
        var otherText = await workspace.GetDocumentText(other);
        var mainText = await workspace.GetDocumentText("Test0.cs");
        otherText.ShouldBe("using System;\npublic sealed class Fixed { }");
        mainText.ShouldBe("using System;\npublic sealed class Main { }");
    }

    [Fact]
    public async Task A_project_reference_lets_the_main_document_use_the_module()
    {
        // Arrange
        var workspace = CodeFixWorkspace.CreateWithProjectReference(
            "public sealed class Primary { public Module Module { get; } = new(); }",
            "public sealed class Module { }",
            "Sample.Module",
            new CodeFixWorkspaceOptions { AssemblyName = "Sample.Primary" });

        // Act
        var diagnostics = await workspace.GetCompilationDiagnostics();

        // Assert
        diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        workspace.Project.AssemblyName.ShouldBe("Sample.Primary");
        workspace.Document.Name.ShouldBe("Primary.cs");
    }

    [Fact]
    public async Task A_marker_that_the_document_lacks_is_reported()
    {
        // Arrange
        var workspace = CodeFixWorkspace.Create("public sealed class Sample { }");
        Func<Task> create = () => workspace.CreateDocumentDiagnostic(ReplacingCodeFix.Id, "Missing");

        // Act
        var exception = await create.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Could not find marker text 'Missing' in document 'Test0.cs'.");
    }
}
