extern alias testingcodefixes;

using System.Collections.Immutable;

namespace SharedKernel.Testing.Analyzers.Tests;

public sealed class SharedKernelTestingCodeFixProviderTests
{
    private const string WarningSuppressionDiagnosticId = TestingDiagnosticIds.TestMethodWarningSuppression;
    private const string XunitMethodNamingDiagnosticId = TestingDiagnosticIds.XunitTestMethodNaming;
    private const string XunitRequiredTraitDiagnosticId = TestingDiagnosticIds.XunitTestMethodRequiredTrait;
    private const string XunitHelperMethodDiagnosticId = TestingDiagnosticIds.XunitTestClassHelperMethod;
    private const string XunitSerialJustificationDiagnosticId = TestingDiagnosticIds.XunitSerialCollectionJustification;
    private const string XunitAssertionWrapperDiagnosticId = TestingDiagnosticIds.XunitAssertionWrapper;
    private const string XunitTraitConstantUsageDiagnosticId = TestingDiagnosticIds.XunitTraitConstantUsage;
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
    public void Provider_advertises_assertion_wrapper_diagnostic()
    {
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        provider.FixableDiagnosticIds.ShouldContain(XunitAssertionWrapperDiagnosticId);
    }

    [Fact]
    public void Provider_advertises_trait_constant_usage_diagnostic()
    {
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        provider.FixableDiagnosticIds.ShouldContain(XunitTraitConstantUsageDiagnosticId);
    }

    [Fact]
    public void Provider_advertises_helper_method_diagnostic()
    {
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        provider.FixableDiagnosticIds.ShouldContain(XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Helper_method_fix_moves_static_helper_to_dedicated_document()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    var id = CreateTourId();
                }

                private static int CreateTourId()
                {
                    return 42;
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitHelperMethodDiagnosticId,
            "private static int CreateTourId()");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var testDocumentText = await workspace.GetDocumentText();
        var helperDocumentText = await workspace.GetDocumentText("TourLoaderTestsHelpers.cs");

        // Assert
        testDocumentText.ShouldContain("var id = TourLoaderTestsHelpers.CreateTourId();", StringComparison.Ordinal);
        testDocumentText.ShouldNotContain("private static int CreateTourId()");
        helperDocumentText.ShouldContain("namespace Demo;", StringComparison.Ordinal);
        helperDocumentText.ShouldContain("internal static class TourLoaderTestsHelpers", StringComparison.Ordinal);
        helperDocumentText.ShouldContain("internal static int CreateTourId()", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Helper_method_fix_is_not_offered_for_instance_helper()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    var id = CreateTourId();
                }

                private int CreateTourId()
                {
                    return 42;
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitHelperMethodDiagnosticId,
            "private int CreateTourId()");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        codeActions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Helper_method_fix_is_not_offered_when_static_helper_uses_test_class_member()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                private static readonly int Seed = 42;

                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    var id = CreateTourId();
                }

                private static int CreateTourId()
                {
                    return Seed;
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitHelperMethodDiagnosticId,
            "private static int CreateTourId()");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        codeActions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Helper_method_fix_is_not_offered_for_non_private_static_helper()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    var id = CreateTourId();
                }

                internal static int CreateTourId()
                {
                    return 42;
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitHelperMethodDiagnosticId,
            "internal static int CreateTourId()");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        codeActions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Helper_method_fix_is_not_offered_when_helper_file_exists()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    var id = CreateTourId();
                }

                private static int CreateTourId()
                {
                    return 42;
                }
            }
            """;

        using var tempDirectory = TemporaryCodeFixDirectory.Create();
        var sourcePath = Path.Combine(tempDirectory.Path, "TourLoaderTests.cs");
        var helperFilePath = Path.Combine(tempDirectory.Path, "TourLoaderTestsHelpers.cs");
        await File.WriteAllTextAsync(helperFilePath, string.Empty, TestContext.Current.CancellationToken);
        var workspace = CodeFixTestWorkspace.Create(source, filePath: sourcePath);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitHelperMethodDiagnosticId,
            "private static int CreateTourId()");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        codeActions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("var reference = (System.Func<int>)CreateTourId;", "private static int CreateTourId()")]
    [InlineData("var name = nameof(CreateTourId);", "private static int CreateTourId()")]
    [InlineData("var id = TourLoaderTests.CreateTourId();", "private static int CreateTourId()")]
    [InlineData("var id = CreateTourId<int>();", "private static T CreateTourId<T>()")]
    public async Task Helper_method_fix_is_not_offered_for_non_rewriteable_helper_reference(
        string helperUsage,
        string helperDeclaration)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    {{helperUsage}}
                }

                {{helperDeclaration}}
                {
                    return default!;
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitHelperMethodDiagnosticId,
            helperDeclaration);

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        codeActions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Helper_method_fix_is_not_offered_when_same_named_invocation_binds_to_another_symbol()
    {
        // Arrange
        const string source = """
            using static Demo.OtherTourHelpers;

            namespace Demo;

            public sealed class CreateTourId;

            public static class OtherTourHelpers
            {
                public static int CreateTourId()
                {
                    return 7;
                }
            }

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    var id = CreateTourId();
                }

                private static int CreateTourId(string value)
                {
                    return 42;
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitHelperMethodDiagnosticId,
            "private static int CreateTourId(string value)");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        codeActions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Helper_method_fix_keeps_scanning_after_same_named_non_invocation_binds_to_another_symbol()
    {
        // Arrange
        const string source = """
            using static Demo.OtherTourHelpers;

            namespace Demo;

            public static class OtherTourHelpers
            {
                public static int CreateTourId()
                {
                    return 7;
                }
            }

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    var type = typeof(CreateTourId);
                    var id = OtherTourHelpers.CreateTourId();
                }

                private static int CreateTourId(string value)
                {
                    return 42;
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitHelperMethodDiagnosticId,
            "private static int CreateTourId(string value)");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        codeActions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Helper_method_fix_is_offered_when_nameof_binds_to_another_symbol()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class CreateTourId;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    var name = nameof(CreateTourId);
                    var id = CreateTourId("tour");
                }

                private static int CreateTourId(string value)
                {
                    return 42;
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitHelperMethodDiagnosticId,
            "private static int CreateTourId(string value)");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        codeActions.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Trait_constant_fix_replaces_literal_with_configured_constant()
    {
        // Arrange
        const string source = """
            namespace SharedKernel.Testing
            {
                public static class TestTraitNames
                {
                    public const string CategoryName = "Category";
                }
            }

            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                [Trait("Category", "Smoke")]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var analyzerDiagnostics = await workspace.GetAnalyzerDiagnostics(new SharedKernelTestingAnalyzer());
        var diagnostic = analyzerDiagnostics.ShouldHaveSingleItem(static candidate => candidate.Id == XunitTraitConstantUsageDiagnosticId);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();

        diagnostic.Properties["Replacement"].ShouldBe("global::SharedKernel.Testing.TestTraitNames.CategoryName");

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        updatedText.ShouldContain("[Trait(global::SharedKernel.Testing.TestTraitNames.CategoryName, \"Smoke\")]", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trait_constant_fix_is_not_offered_when_replacement_property_is_missing()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                [Trait("Category", "Smoke")]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            XunitTraitConstantUsageDiagnosticId,
            "\"Category\"");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        codeActions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Assert.All(new[] { 1 }, value => Assert.True(value > 0))", "(new[] { 1 }).ShouldAllSatisfy(value => Assert.True(value > 0))")]
    [InlineData("Assert.Contains(new[] { 1 }, value => value > 0)", "(new[] { 1 }).ShouldContain(value => value > 0)")]
    [InlineData("Assert.Contains(1, new[] { 1 })", "(new[] { 1 }).ShouldContain(1)")]
    [InlineData("Assert.Contains(\"a\", new[] { \"A\" }, StringComparer.OrdinalIgnoreCase)", "(new[] { \"A\" }).ShouldContain(\"a\", StringComparer.OrdinalIgnoreCase)")]
    [InlineData("Assert.DoesNotContain(2, new[] { 1 })", "(new[] { 1 }).ShouldNotContain(2)")]
    [InlineData("Assert.Empty(Array.Empty<int>())", "Array.Empty<int>().ShouldBeEmpty()")]
    [InlineData("Assert.True(true)", "(true).ShouldBeTrue()")]
    [InlineData("Assert.True(true, \"message\")", "(true).ShouldBeTrue(\"message\")")]
    [InlineData("Assert.False(false)", "(false).ShouldBeFalse()")]
    [InlineData("Assert.False(false, \"message\")", "(false).ShouldBeFalse(\"message\")")]
    [InlineData("Assert.InRange(2, 1, 3)", "(2).ShouldBeInRange(1, 3)")]
    [InlineData("Assert.NotEmpty(new[] { 1 })", "(new[] { 1 }).ShouldNotBeEmpty()")]
    [InlineData("Assert.NotEqual(1, 2)", "(2).ShouldNotBe(1)")]
    [InlineData("Assert.NotEqual(\"a\", \"b\", StringComparer.Ordinal)", "(\"b\").ShouldNotBe(\"a\", StringComparer.Ordinal)")]
    [InlineData("Assert.NotNull(new object())", "(new object()).ShouldNotBeNull()")]
    [InlineData("Assert.Null(default(object))", "(default(object)).ShouldBeNull()")]
    [InlineData("Assert.Same(new object(), new object())", "(new object()).ShouldBeSameAs(new object())")]
    [InlineData("Assert.Single(new[] { 1 })", "(new[] { 1 }).ShouldHaveSingleItem()")]
    [InlineData("Assert.Equal(\"a\", \"A\", ignoreCase: true)", "(\"A\").ShouldBe(\"a\", System.StringComparer.OrdinalIgnoreCase)")]
    [InlineData("Assert.Equal(\"a\", \"A\", ignoreCase: false)", "(\"A\").ShouldBe(\"a\", System.StringComparer.Ordinal)")]
    [InlineData("Assert.Equal(\"a\", \"A\", ignoreCase: compareCase)", "(\"A\").ShouldBe(\"a\", (compareCase) ? System.StringComparer.OrdinalIgnoreCase : System.StringComparer.Ordinal)")]
    [InlineData("Assert.Equal(true, compareCase)", "compareCase.ShouldBe(true)")]
    public async Task Assertion_wrapper_fix_rewrites_supported_assertions(string assertion, string expectedRewrite)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            public sealed class AssertionWrapperTests
            {
                public void Execute(bool compareCase)
                {
                    {{assertion}};
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var sourceText = await workspace.GetDocumentText();
        var diagnostics = await workspace.GetAnalyzerDiagnostics(new SharedKernelTestingAnalyzer());
        var diagnostic = diagnostics
            .Where(candidate => candidate.Id == XunitAssertionWrapperDiagnosticId)
            .ShouldHaveSingleItem(candidate => string.Equals(sourceText.Substring(candidate.Location.SourceSpan.Start, candidate.Location.SourceSpan.Length), assertion, StringComparison.Ordinal));

        // Act
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        updatedText.ShouldContain("using SharedKernel.Testing.Assertions;", StringComparison.Ordinal);
        updatedText.ShouldContain(expectedRewrite, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Assertion_wrapper_fix_is_not_offered_for_unsupported_assertions()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AssertionWrapperTests
            {
                public void Execute()
                {
                    Assert.Multiple(() => Assert.True(true));
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitAssertionWrapperDiagnosticId, "Assert.Multiple");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        codeActions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Assertion_wrapper_fix_is_not_offered_for_positional_equal_ignore_case()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AssertionWrapperTests
            {
                public void Execute()
                {
                    Assert.Equal("a", "A", true);
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitAssertionWrapperDiagnosticId, "Assert.Equal");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Assertion_wrapper_fix_is_not_offered_for_single_predicate_overload()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AssertionWrapperTests
            {
                public void Execute()
                {
                    Assert.Single(new[] { 1 }, value => value > 0);
                }
            }
            """;

        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.SharedKernelTestingCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(XunitAssertionWrapperDiagnosticId, "Assert.Single");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
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
