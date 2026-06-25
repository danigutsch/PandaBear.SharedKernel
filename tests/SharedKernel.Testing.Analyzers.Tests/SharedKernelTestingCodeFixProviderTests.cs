extern alias testingcodefixes;

using System.Collections.Immutable;

namespace SharedKernel.Testing.Analyzers.Tests;

public sealed class SharedKernelTestingCodeFixProviderTests
{
    private const string WarningSuppressionDiagnosticId = TestingDiagnosticIds.TestMethodWarningSuppression;
    private const string XunitMethodNamingDiagnosticId = TestingDiagnosticIds.XunitTestMethodNaming;
    private const string XunitRequiredTraitDiagnosticId = TestingDiagnosticIds.XunitTestMethodRequiredTrait;
    private const string XunitHelperMethodDiagnosticId = TestingDiagnosticIds.XunitTestClassHelperMethod;
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
    public void Provider_Advertises_Helper_Method_Diagnostic()
    {
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        Assert.Contains(XunitHelperMethodDiagnosticId, provider.FixableDiagnosticIds);
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
    public async Task Helper_Method_Fix_Moves_Static_Helper_To_Nested_Helper_Type()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTour();
                }

                private static string CreateTour()
                {
                    return "tour";
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitHelperMethodDiagnosticId, "private static string CreateTour");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains("TourLoaderTestsHelpers.CreateTour();", updatedText, StringComparison.Ordinal);
        Assert.Contains("private static class TourLoaderTestsHelpers", updatedText, StringComparison.Ordinal);
        Assert.Contains("public static string CreateTour()", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Helper_Method_Fix_Is_Not_Offered_For_Instance_Helper()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTour();
                }

                private string CreateTour()
                {
                    return "tour";
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitHelperMethodDiagnosticId, "private string CreateTour");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Helper_Method_Fix_Is_Not_Offered_When_Qualified_Call_Would_Remain()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    TourLoaderTests.CreateTour();
                }

                [global::Xunit.Fact]
                public void Updates_a_tour_when_the_request_is_valid()
                {
                    TourLoaderTests.CreateTour();
                }

                private static string CreateTour()
                {
                    return "tour";
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitHelperMethodDiagnosticId, "private static string CreateTour");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Helper_Method_Fix_Is_Not_Offered_When_Nested_Type_Call_Would_Remain()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTour();
                }

                [global::Xunit.Fact]
                public void Updates_a_tour_when_the_request_is_valid()
                {
                    NestedBuilder.Build();
                }

                private static string CreateTour()
                {
                    return "tour";
                }

                private static class NestedBuilder
                {
                    public static string Build()
                    {
                        return CreateTour();
                    }
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitHelperMethodDiagnosticId, "private static string CreateTour");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Helper_Method_Fix_Reuses_Existing_Nested_Helper_Type()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTour();
                }

                private static string CreateTour()
                {
                    return "tour";
                }

                private static class TourLoaderTestsHelpers
                {
                    public static string CreateCustomer()
                    {
                        return "customer";
                    }
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitHelperMethodDiagnosticId, "private static string CreateTour");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains("TourLoaderTestsHelpers.CreateTour();", updatedText, StringComparison.Ordinal);
        Assert.Contains("public static string CreateCustomer()", updatedText, StringComparison.Ordinal);
        Assert.Contains("public static string CreateTour()", updatedText, StringComparison.Ordinal);
        var firstHelperTypeIndex = updatedText.IndexOf("private static class TourLoaderTestsHelpers", StringComparison.Ordinal);
        Assert.True(firstHelperTypeIndex >= 0);
        Assert.Equal(
            -1,
            updatedText.IndexOf("private static class TourLoaderTestsHelpers", firstHelperTypeIndex + 1, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Helper_Method_Fix_Is_Not_Offered_When_Existing_Nested_Helper_Method_Has_Same_Name()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTour();
                }

                [global::Xunit.Fact]
                public void Updates_a_tour_when_the_request_is_valid()
                {
                    CreateTour();
                }

                private static string CreateTour()
                {
                    return "tour";
                }

                private static class TourLoaderTestsHelpers
                {
                    public static string CreateTour()
                    {
                        return "existing";
                    }
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitHelperMethodDiagnosticId, "private static string CreateTour");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Helper_Method_Fix_Does_Not_Qualify_Nested_Type_Invocations_With_Same_Name()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTour();
                }

                [global::Xunit.Fact]
                public void Updates_a_tour_when_the_request_is_valid()
                {
                    CreateTour();
                }

                private static string CreateTour()
                {
                    return "tour";
                }

                private static class NestedBuilder
                {
                    public static string Build()
                    {
                        return CreateTour();
                    }

                    private static string CreateTour()
                    {
                        return "nested";
                    }
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitHelperMethodDiagnosticId, "private static string CreateTour");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains("TourLoaderTestsHelpers.CreateTour();", updatedText, StringComparison.Ordinal);
        Assert.Contains("return CreateTour();", updatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("return TourLoaderTestsHelpers.CreateTour();", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Helper_Method_Fix_Does_Not_Qualify_Local_Function_Invocations_With_Same_Name()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTour();
                }

                [global::Xunit.Fact]
                public void Updates_a_tour_when_the_request_is_valid()
                {
                    var value = CreateTour();
                    Assert.Equal("local", value);

                    static string CreateTour()
                    {
                        return "local";
                    }
                }

                private static string CreateTour()
                {
                    return "tour";
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitHelperMethodDiagnosticId, "private static string CreateTour");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains("TourLoaderTestsHelpers.CreateTour();", updatedText, StringComparison.Ordinal);
        Assert.Contains("var value = CreateTour();", updatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("var value = TourLoaderTestsHelpers.CreateTour();", updatedText, StringComparison.Ordinal);
    }
}
