using System.Collections.Immutable;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Tests the SKTEST018 rule that requires one valid test scope on every xUnit test method.</summary>
public sealed class TestScopeAnalyzerTests
{
    private const string DiagnosticId = TestingDiagnosticIds.XunitTestScope;

    [Fact]
    public async Task Test_without_any_scope_reports_SKTEST018()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourTests
            {
                [Fact]
                public void Creates_a_tour()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Info);
        diagnostic.Properties["Reason"].ShouldBe("missing");
    }

    [Fact]
    public async Task Theory_without_any_scope_reports_SKTEST018()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourTests
            {
                [Theory]
                [InlineData(1)]
                public void Creates_a_tour(int count)
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("[TestScope(TestScope.Unit)]", "")]
    [InlineData("", "[TestScope(TestScope.Unit)]")]
    [InlineData("[Trait(\"Scope\", \"unit\")]", "")]
    [InlineData("", "[Trait(\"Scope\", \"unit\")]")]
    public async Task Scope_on_class_or_method_satisfies_the_rule(string classAttribute, string methodAttribute)
    {
        // Arrange
        var source = $$"""
            using PandaBear.Testing;

            namespace Demo;

            {{classAttribute}}
            public sealed class TourTests
            {
                {{methodAttribute}}
                [Fact]
                public void Creates_a_tour()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("[assembly: TestScope(TestScope.Unit)]")]
    [InlineData("[assembly: Trait(\"Scope\", \"unit\")]")]
    public async Task Assembly_level_scope_satisfies_the_rule(string assemblyAttribute)
    {
        // Arrange
        var source = $$"""
            using PandaBear.Testing;

            {{assemblyAttribute}}

            namespace Demo;

            public sealed class TourTests
            {
                [Fact]
                public void Creates_a_tour()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Scope_on_an_enclosing_class_satisfies_a_nested_test_class()
    {
        // Arrange
        const string source = """
            using PandaBear.Testing;

            namespace Demo;

            [TestScope(TestScope.Unit)]
            public sealed class TourTests
            {
                public sealed class Creation
                {
                    [Fact]
                    public void Creates_a_tour()
                    {
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Scope_on_an_abstract_base_class_satisfies_derived_tests()
    {
        // Arrange
        const string source = """
            using PandaBear.Testing;

            namespace Demo;

            [TestScope(TestScope.Integration)]
            public abstract class StoreTestsBase
            {
            }

            public sealed class TourStoreTests : StoreTestsBase
            {
                [Fact]
                public void Saves_a_tour()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Test_in_an_unscoped_abstract_base_class_reports_SKTEST018()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public abstract class StoreTestsBase
            {
                [Fact]
                public void Saves_a_tour()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Class_without_test_methods_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourHelper
            {
                public void Creates_a_tour()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Trait_scope_outside_the_allowed_set_reports_unknown()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [Trait("Scope", "bogus")]
            public sealed class TourTests
            {
                [Fact]
                public void Creates_a_tour()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Properties["Reason"].ShouldBe("unknown");
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldContain("bogus", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Undefined_typed_scope_reports_unknown()
    {
        // Arrange
        const string source = """
            using PandaBear.Testing;

            namespace Demo;

            [TestScope((TestScope)99)]
            public sealed class TourTests
            {
                [Fact]
                public void Creates_a_tour()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Properties["Reason"].ShouldBe("unknown");
    }

    [Fact]
    public async Task Allowed_scopes_option_narrows_the_valid_set()
    {
        // Arrange
        const string source = """
            using PandaBear.Testing;

            namespace Demo;

            [TestScope(TestScope.System)]
            public sealed class TourTests
            {
                [Fact]
                public void Creates_a_tour()
                {
                }
            }
            """;
        var options = ImmutableDictionary<string, string>.Empty
            .Add("sharedkernel_testing_allowed_scopes", "unit;integration");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source, options);

        // Assert
        diagnostics.ShouldHaveSingleItem().Properties["Reason"].ShouldBe("unknown");
    }

    [Fact]
    public async Task Different_scopes_on_one_level_report_conflicting()
    {
        // Arrange
        const string source = """
            using PandaBear.Testing;

            namespace Demo;

            [TestScope(TestScope.Unit)]
            [Trait("Scope", "integration")]
            public sealed class TourTests
            {
                [Fact]
                public void Creates_a_tour()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Properties["Reason"].ShouldBe("conflicting");
    }

    [Fact]
    public async Task Method_scope_overrides_a_different_class_scope()
    {
        // Arrange
        const string source = """
            using PandaBear.Testing;

            namespace Demo;

            [TestScope(TestScope.Unit)]
            public sealed class TourTests
            {
                [TestScope(TestScope.Integration)]
                [Fact]
                public void Creates_a_tour()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Same_scope_declared_twice_on_one_level_is_not_a_conflict()
    {
        // Arrange
        const string source = """
            using PandaBear.Testing;

            namespace Demo;

            [TestScope(TestScope.Unit)]
            [Trait("Scope", "unit")]
            public sealed class TourTests
            {
                [Fact]
                public void Creates_a_tour()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
