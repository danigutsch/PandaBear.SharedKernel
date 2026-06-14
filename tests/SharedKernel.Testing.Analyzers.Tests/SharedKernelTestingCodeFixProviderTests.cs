using SharedKernel.Testing.CodeFixes;

namespace SharedKernel.Testing.Analyzers.Tests;

public sealed class SharedKernelTestingCodeFixProviderTests
{
    private const string XunitMethodNamingDiagnosticId = "SKTEST002";

    [Fact]
    public async Task Test_Naming_Fix_Renames_Method_And_Reference_Correctly()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void CreatesATourWhenTheRequestIsValid()
                {
                }

                public void Execute()
                {
                    CreatesATourWhenTheRequestIsValid();
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitMethodNamingDiagnosticId, "CreatesATourWhenTheRequestIsValid()");

        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        Assert.Contains("Creates_A_Tour_When_The_Request_Is_Valid()", updatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatesATourWhenTheRequestIsValid()", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test_Naming_Fix_Is_Not_Offered_When_Target_Name_Would_Conflict_With_Existing_Method()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void CreatesATourWhenTheRequestIsValid()
                {
                }

                public void Creates_A_Tour_When_The_Request_Is_Valid()
                {
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitMethodNamingDiagnosticId, "CreatesATourWhenTheRequestIsValid()");

        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        Assert.Empty(codeActions);
    }
}
