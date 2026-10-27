namespace PandaBear.Style.CodeFixes.Tests;

/// <summary>Tests the SKSTYLE016 code fix that renames a file to match its top-level type.</summary>
public sealed class RenameFileCodeFixTests
{
    [Fact]
    public async Task File_name_fix_renames_file_to_type_name()
    {
        // Arrange
        var workspace = CodeFixTestWorkspace.Create("namespace Demo;\n\npublic sealed class Tour\n{\n}\n");
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.FileNameMismatch, "public sealed class Tour");

        // Act
        var actions = await workspace.GetCodeActions(new SharedKernelStyleCodeFixProvider(), diagnostic);
        var action = actions.ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(action);

        // Assert
        var document = workspace.Workspace.CurrentSolution.Projects.ShouldHaveSingleItem().Documents.ShouldHaveSingleItem();
        document.Name.ShouldBe("Tour.cs");
        document.FilePath.ShouldBe("/Tour.cs");
    }

    [Fact]
    public async Task File_name_fix_uses_type_parameters_when_plain_name_is_taken()
    {
        // Arrange
        var workspace = CodeFixTestWorkspace.Create("namespace Demo;\n\npublic sealed class Box<T>\n{\n}\n");
        workspace.AddDocument("namespace Demo;\n\npublic sealed class Box\n{\n}\n", "Box.cs");
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.FileNameMismatch, "public sealed class Box<T>");

        // Act
        var actions = await workspace.GetCodeActions(new SharedKernelStyleCodeFixProvider(), diagnostic);
        var action = actions.ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(action);

        // Assert
        var documents = workspace.Workspace.CurrentSolution.Projects.ShouldHaveSingleItem().Documents;
        documents.ShouldContain(static document => document.Name == "Box{T}.cs");
    }

    [Fact]
    public async Task File_name_fix_is_not_offered_when_every_target_name_exists()
    {
        // Arrange
        var workspace = CodeFixTestWorkspace.Create("namespace Demo;\n\npublic sealed class Tour\n{\n}\n");
        workspace.AddDocument("namespace Demo;\n\npublic sealed class Other\n{\n}\n", "Tour.cs");
        var diagnostic = await workspace.CreateDocumentDiagnostic(Analyzers.StyleDiagnosticIds.FileNameMismatch, "public sealed class Tour");

        // Act
        var actions = await workspace.GetCodeActions(new SharedKernelStyleCodeFixProvider(), diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }
}
