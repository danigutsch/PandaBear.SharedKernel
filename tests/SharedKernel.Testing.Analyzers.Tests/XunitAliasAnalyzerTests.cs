using Microsoft.CodeAnalysis;

namespace SharedKernel.Testing.Analyzers.Tests;

/// <summary>Tests analyzer classification of aliased and unrelated test attributes.</summary>
public sealed class XunitAliasAnalyzerTests
{
    [Fact]
    public async Task Aliased_tests_and_ordinary_tests_keep_helper_classification()
    {
        // Arrange
        const string source = """
            using FactCase = Xunit.FactAttribute;
            using TheoryCase = Xunit.TheoryAttribute;

            public sealed class SampleTests
            {
                [FactCase]
                public void Checks_an_aliased_fact()
                {
                    _ = CreateLocalValue();
                    static int CreateLocalValue() => 42;
                }

                [TheoryCase, InlineData(42)]
                public void Checks_an_aliased_theory(int value)
                {
                }

                [Fact]
                public void Checks_an_ordinary_fact()
                {
                }

                private static int CreateSharedValue() => 42;
            }
            """;

        // Act
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilerErrors = AnalyzerTestHarness.CreateCompilation(source).GetDiagnostics(cancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(TestingDiagnosticIds.XunitTestClassHelperMethod, source);
        var declarations = await Task.WhenAll(diagnostics.Select(async diagnostic =>
        {
            var text = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(cancellationToken);
            return text.ToString(diagnostic.Location.SourceSpan);
        }));

        // Assert
        string[] expected = ["CreateLocalValue", "CreateSharedValue"];
        compilerErrors.ShouldBeEmpty();
        declarations.Order(StringComparer.Ordinal).ToArray().ShouldBe(expected);
    }

    [Fact]
    public async Task Aliased_test_method_keeps_test_specific_rules()
    {
        // Arrange
        const string source = """
            using FactCase = Xunit.FactAttribute;

            public sealed class SampleTests
            {
                [FactCase]
                public void UnconventionalName()
                {
                    try { }
                    finally { }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldContain(static diagnostic => diagnostic.Id == TestingDiagnosticIds.XunitTestMethodNaming);
        diagnostics.ShouldContain(static diagnostic => diagnostic.Id == TestingDiagnosticIds.XunitTryFinallyCleanup);
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == TestingDiagnosticIds.XunitTestClassHelperMethod);
    }

    [Fact]
    public async Task Alias_for_unrelated_attribute_keeps_ordinary_methods_outside_xUnit_rules()
    {
        // Arrange
        const string source = """
            using FactCase = Other.FactAttribute;

            public sealed class SampleTests
            {
                [Xunit.Fact]
                public void Checks_an_ordinary_fact()
                {
                }

                [FactCase]
                public int CreateSharedValue() => 42;
            }

            public sealed class OrdinaryType
            {
                [FactCase]
                public int ReadValue()
                {
                    return ReadLocalValue();
                    static int ReadLocalValue() => 42;
                }
            }

            namespace Other
            {
                public sealed class FactAttribute : Attribute;
            }
            """;

        // Act
        var compilerErrors = AnalyzerTestHarness.CreateCompilation(source).GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        compilerErrors.ShouldBeEmpty();
        var helper = diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning).ShouldHaveSingleItem();
        helper.Id.ShouldBe(TestingDiagnosticIds.XunitTestClassHelperMethod);
        var text = await helper.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        text.ToString(helper.Location.SourceSpan).ShouldBe("CreateSharedValue");
    }
}
