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
    public async Task Test_naming_fix_renames_method_and_reference_correctly()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
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
    public async Task Test_naming_fix_converts_title_cased_segments_to_sentence_style()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Some_Title()
                {
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitMethodNamingDiagnosticId, "Some_Title()");

        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        Assert.Contains("Some_title()", updatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Some_Title()", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test_naming_fix_is_not_offered_when_target_name_would_conflict_with_existing_method()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
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
    public async Task Test_naming_fix_is_not_offered_when_target_name_would_conflict_with_existing_property()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
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
    public async Task Test_naming_fix_is_not_offered_when_name_cannot_be_safely_split()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
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
    public async Task Test_naming_fix_splits_acronym_and_digits_into_underscore_form()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
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
    public void Fix_all_is_not_advertised_for_testing_code_fixes()
    {
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        Assert.Empty(provider.GetFixAllProvider().GetSupportedFixAllScopes());
    }

    [Fact]
    public void Provider_advertises_warning_suppression_diagnostic()
    {
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        Assert.Contains(WarningSuppressionDiagnosticId, provider.FixableDiagnosticIds);
    }

    [Fact]
    public async Task Warning_suppression_fix_removes_pragma_directive()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Uses_local_warning_suppression()
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
    public async Task Required_trait_fix_adds_configured_trait_to_method()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
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
    public async Task Required_trait_fix_is_not_offered_when_diagnostic_lacks_trait_properties()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
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
    public void Provider_advertises_serial_justification_diagnostic()
    {
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        Assert.Contains(XunitSerialJustificationDiagnosticId, provider.FixableDiagnosticIds);
    }

    [Fact]
    public async Task Serial_justification_fix_adds_placeholder_attribute_to_collection_class()
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
        Assert.True(
            updatedText.IndexOf("SerialTestJustification", StringComparison.Ordinal) < updatedText.IndexOf("CollectionDefinition", StringComparison.Ordinal),
            "Expected serial justification to be inserted before the collection definition.");
    }

    [Fact]
    public async Task Serial_justification_fix_adds_placeholder_attribute_to_collection_record()
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
        Assert.True(
            updatedText.IndexOf("SerialTestJustification", StringComparison.Ordinal) < updatedText.IndexOf("CollectionDefinition", StringComparison.Ordinal),
            "Expected serial justification to be inserted before the collection definition.");
    }

}
