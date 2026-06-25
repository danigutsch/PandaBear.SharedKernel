using System.Collections.Immutable;

namespace SharedKernel.Testing.Analyzers.Tests;

public sealed class SharedKernelTestingAnalyzerTests
{
    private const string WarningSuppressionDiagnosticId = TestingDiagnosticIds.TestMethodWarningSuppression;
    private const string XunitMethodNamingDiagnosticId = TestingDiagnosticIds.XunitTestMethodNaming;
    private const string XunitRequiredTraitDiagnosticId = TestingDiagnosticIds.XunitTestMethodRequiredTrait;

    [Fact]
    public async Task Pragma_Warning_Disable_Inside_Fact_Method_Reports_SKTEST001()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Uses_Local_Warning_Suppression()
                {
                    #pragma warning disable CA1822
                    var value = 42;
                    #pragma warning restore CA1822
                    Assert.Equal(42, value);
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        Assert.Equal(
            2,
            diagnostics.Count(static candidate => candidate.Id == WarningSuppressionDiagnosticId));
    }

    [Fact]
    public async Task Pragma_Warning_Disable_Outside_Test_Method_Does_Not_Report_SKTEST001()
    {
        const string source = """
            namespace Demo;

            #pragma warning disable CA1822
            public sealed class TourLoader
            {
                public void Execute()
                {
                }
            }
            #pragma warning restore CA1822
            """;

        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == WarningSuppressionDiagnosticId);
    }

    [Fact]
    public async Task Pragma_Warning_Disable_Inside_Non_Test_Method_Does_Not_Report_SKTEST001()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public void Execute()
                {
                    #pragma warning disable CA1822
                    var value = 42;
                    #pragma warning restore CA1822
                    _ = value;
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == WarningSuppressionDiagnosticId);
    }

    [Fact]
    public async Task Pragma_Warning_Disable_Inside_Theory_Method_Reports_SKTEST001()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Theory]
                [InlineData(42)]
                public void Uses_Local_Warning_Suppression(int value)
                {
                    #pragma warning disable CA1822
                    Assert.Equal(42, value);
                    #pragma warning restore CA1822
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        Assert.Equal(
            2,
            diagnostics.Count(static candidate => candidate.Id == WarningSuppressionDiagnosticId));
    }

    [Fact]
    public async Task Test_Naming_Without_Underscores_Reports_S_K_T_E_S_T002()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void UsesLocalWarningSuppression()
                {
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitMethodNamingDiagnosticId);
    }

    [Fact]
    public async Task Default_Test_Naming_With_Underscores_Does_Not_Report_S_K_T_E_S_T002()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Uses_local_warning_suppression()
                {
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitMethodNamingDiagnosticId);
    }

    [Fact]
    public async Task Fact_Attribute_Form_Without_Underscores_Reports_S_K_T_E_S_T002()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.FactAttribute]
                public void UsesLocalWarningSuppression()
                {
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitMethodNamingDiagnosticId);
    }

    [Fact]
    public async Task Required_Trait_Config_Missing_From_Test_Method_Reports_S_K_T_E_S_T003()
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
        var options = ImmutableDictionary<string, string>.Empty
            .Add("sharedkernel_testing_required_traits", "Category=Smoke");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, analyzerOptions: options);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitRequiredTraitDiagnosticId);
    }

    [Fact]
    public async Task Required_Trait_Config_Already_On_Method_Does_Not_Report_S_K_T_E_S_T003()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                [global::Xunit.Trait("Category", "Smoke")]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                }
            }
            """;
        var options = ImmutableDictionary<string, string>.Empty
            .Add("sharedkernel_testing_required_traits", "Category=Smoke");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, analyzerOptions: options);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitRequiredTraitDiagnosticId);
    }

    [Fact]
    public async Task Required_Trait_Config_Already_On_Class_Does_Not_Report_S_K_T_E_S_T003()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [global::Xunit.Trait("Category", "Smoke")]
            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                }
            }
            """;
        var options = ImmutableDictionary<string, string>.Empty
            .Add("sharedkernel_testing_required_traits", "Category=Smoke");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, analyzerOptions: options);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitRequiredTraitDiagnosticId);
    }

    [Fact]
    public async Task Required_Trait_Config_Already_On_Assembly_Does_Not_Report_S_K_T_E_S_T003()
    {
        // Arrange
        const string source = """
            [assembly: global::Xunit.Trait("Category", "Smoke")]

            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                }
            }
            """;
        var options = ImmutableDictionary<string, string>.Empty
            .Add("sharedkernel_testing_required_traits", "Category=Smoke");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, analyzerOptions: options);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitRequiredTraitDiagnosticId);
    }

    [Fact]
    public async Task Required_Trait_Config_Multiple_Missing_Traits_Reports_Once_Per_Trait()
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
        var options = ImmutableDictionary<string, string>.Empty
            .Add("sharedkernel_testing_required_traits", "Category=Smoke;Scope=Integration");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, analyzerOptions: options);

        // Assert
        Assert.Equal(2, diagnostics.Count(static candidate => candidate.Id == XunitRequiredTraitDiagnosticId));
    }
}
