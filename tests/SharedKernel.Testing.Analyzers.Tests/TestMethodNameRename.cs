extern alias testingcodefixes;

using System.Collections.Immutable;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Applies the SKTEST002 rename fix to a single fact.</summary>
internal static class TestMethodNameRename
{
    /// <summary>Renames the fact through the code fix and returns the updated document text.</summary>
    /// <param name="methodName">Current name of the fact.</param>
    /// <param name="properties">Properties of the SKTEST002 diagnostic, or null.</param>
    public static async Task<string> Apply(string methodName, ImmutableDictionary<string, string?>? properties)
    {
        var workspace = CodeFixTestWorkspace.Create(TestMethodNameTestSource.Create(methodName));
        var provider = new testingcodefixes::PandaBear.Testing.CodeFixes.TestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(TestingDiagnosticIds.XunitTestMethodNaming, methodName + "()", properties).ConfigureAwait(false);
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic).ConfigureAwait(false)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction).ConfigureAwait(false);

        return await workspace.GetDocumentText().ConfigureAwait(false);
    }
}
