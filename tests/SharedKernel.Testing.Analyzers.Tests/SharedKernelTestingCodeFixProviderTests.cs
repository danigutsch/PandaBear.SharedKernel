extern alias testingcodefixes;

using System.Collections.Immutable;

namespace SharedKernel.Testing.Analyzers.Tests;

public sealed class SharedKernelTestingCodeFixProviderTests
{
    private const string WarningSuppressionDiagnosticId = TestingDiagnosticIds.TestMethodWarningSuppression;
    private const string XunitMethodNamingDiagnosticId = TestingDiagnosticIds.XunitTestMethodNaming;
    private const string XunitRequiredTraitDiagnosticId = TestingDiagnosticIds.XunitTestMethodRequiredTrait;
    private const string XunitSerialJustificationDiagnosticId = TestingDiagnosticIds.XunitSerialCollectionJustification;
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

        Assert.Contains("Creates_a_tour_when_the_request_is_valid()", updatedText, StringComparison.Ordinal);
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

                public void Creates_a_tour_when_the_request_is_valid()
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

                public string Creates_a_tour_when_the_request_is_valid { get; } = string.Empty;
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

        Assert.Contains("Uses_http2_timeout_fallback()", updatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("UsesHTTP2TimeoutFallback", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Fix_All_Is_Not_Advertised_For_Testing_Code_Fixes()
    {
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        Assert.Empty(provider.GetFixAllProvider().GetSupportedFixAllScopes());
    }

    [Fact]
    public void Provider_Advertises_Warning_Suppression_Diagnostic()
    {
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        Assert.Contains(WarningSuppressionDiagnosticId, provider.FixableDiagnosticIds);
    }

    [Fact]
    public async Task Warning_Suppression_Fix_Removes_Pragma_Directive()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Uses_Local_Warning_Suppression()
                {
                    #pragma warning disable CA1822
                    var value = 42;
                    Assert.Equal(42, value);
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(WarningSuppressionDiagnosticId, "#pragma warning disable CA1822");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.DoesNotContain("#pragma warning disable CA1822", updatedText, StringComparison.Ordinal);
        Assert.Contains("var value = 42;", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Required_Trait_Fix_Adds_Configured_Trait_To_Method()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                }
            }
            """;
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add("TraitName", "Category")
            .Add("TraitValue", "Smoke");

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitRequiredTraitDiagnosticId,
            "Creates_a_tour_when_the_request_is_valid()",
            properties);

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains("[global::Xunit.Trait(\"Category\", \"Smoke\")]", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Required_Trait_Fix_Is_Not_Offered_When_Diagnostic_Lacks_Trait_Properties()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitRequiredTraitDiagnosticId,
            "Creates_a_tour_when_the_request_is_valid()");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public void Provider_Advertises_Serial_Justification_Diagnostic()
    {
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        Assert.Contains(XunitSerialJustificationDiagnosticId, provider.FixableDiagnosticIds);
    }

    [Fact]
    public async Task Serial_Justification_Fix_Adds_Placeholder_Attribute_To_Collection_Class()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [global::Xunit.CollectionDefinition("Serial database", DisableParallelization = true)]
            public sealed class SerialDatabaseCollection
            {
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitSerialJustificationDiagnosticId,
            "SerialDatabaseCollection");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains("[global::SharedKernel.Testing.SerialTestJustification(\"TODO: explain why this collection must run serially.\")]", updatedText, StringComparison.Ordinal);
        Assert.Contains("[global::Xunit.CollectionDefinition(\"Serial database\", DisableParallelization = true)]", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Serial_Justification_Fix_Adds_Placeholder_Attribute_To_Collection_Record()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [global::Xunit.CollectionDefinition("Serial database", DisableParallelization = true)]
            public sealed record SerialDatabaseCollection;
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitSerialJustificationDiagnosticId,
            "SerialDatabaseCollection");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains("[global::SharedKernel.Testing.SerialTestJustification(\"TODO: explain why this collection must run serially.\")]", updatedText, StringComparison.Ordinal);
        Assert.Contains("public sealed record SerialDatabaseCollection;", updatedText, StringComparison.Ordinal);
    }

}
