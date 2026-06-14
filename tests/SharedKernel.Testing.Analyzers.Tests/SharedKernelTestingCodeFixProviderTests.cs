extern alias testingcodefixes;

namespace SharedKernel.Testing.Analyzers.Tests;

public sealed class SharedKernelTestingCodeFixProviderTests
{
    private const string XunitMethodNamingDiagnosticId = TestingDiagnosticIds.XunitTestMethodNaming;

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
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
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
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitMethodNamingDiagnosticId, "CreatesATourWhenTheRequestIsValid()");

        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Test_Naming_Fix_Is_Not_Offered_When_Target_Name_Would_Conflict_With_Existing_Property()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void CreatesATourWhenTheRequestIsValid()
                {
                }

                public string Creates_A_Tour_When_The_Request_Is_Valid { get; } = string.Empty;
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitMethodNamingDiagnosticId, "CreatesATourWhenTheRequestIsValid()");

        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Test_Naming_Fix_Is_Not_Offered_When_Name_Cannot_Be_Safely_Split()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Tour()
                {
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitMethodNamingDiagnosticId, "Tour()");

        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Test_Naming_Fix_Splits_Acronym_And_Digits_Into_Underscore_Form()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void UsesHTTP2TimeoutFallback()
                {
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitMethodNamingDiagnosticId, "UsesHTTP2TimeoutFallback()");

        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        Assert.Contains("Uses_HTTP2_Timeout_Fallback()", updatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("UsesHTTP2TimeoutFallback", updatedText, StringComparison.Ordinal);
    }
}
