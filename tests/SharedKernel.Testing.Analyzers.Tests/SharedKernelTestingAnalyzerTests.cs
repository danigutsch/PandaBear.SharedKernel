using System.Collections.Immutable;

namespace SharedKernel.Testing.Analyzers.Tests;

public sealed class SharedKernelTestingAnalyzerTests
{
    private const string WarningSuppressionDiagnosticId = TestingDiagnosticIds.TestMethodWarningSuppression;
    private const string XunitMethodNamingDiagnosticId = TestingDiagnosticIds.XunitTestMethodNaming;
    private const string XunitRequiredTraitDiagnosticId = TestingDiagnosticIds.XunitTestMethodRequiredTrait;
    private const string XunitHelperMethodDiagnosticId = TestingDiagnosticIds.XunitTestClassHelperMethod;
    private const string XunitSerialJustificationDiagnosticId = TestingDiagnosticIds.XunitSerialCollectionJustification;
    [Fact]
    public async Task Pragma_warning_disable_inside_fact_method_reports_SKTEST001()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Uses_local_warning_suppression()
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
    public async Task Pragma_warning_disable_outside_test_method_does_not_report_SKTEST001()
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
    public async Task Pragma_warning_disable_inside_non_test_method_does_not_report_SKTEST001()
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
    public async Task Pragma_warning_disable_inside_theory_method_reports_SKTEST001()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Theory]
                [InlineData(42)]
                public void Uses_local_warning_suppression(int value)
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
    public async Task Test_naming_without_underscores_reports_s_k_t_e_s_t002()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void UsesLocalWarningSuppression()
                {
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitMethodNamingDiagnosticId);
    }

    [Fact]
    public async Task Default_test_naming_with_underscores_does_not_report_s_k_t_e_s_t002()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Uses_local_warning_suppression()
                {
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitMethodNamingDiagnosticId);
    }

    [Fact]
    public async Task Strict_test_naming_with_title_cased_later_segment_reports_s_k_t_e_s_t002()
    {
        // Arrange
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
        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitMethodNamingDiagnosticId);
    }

    [Fact]
    public async Task Strict_test_naming_with_uppercase_later_segment_reports_s_k_t_e_s_t002()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Some_TITLE()
                {
                }
            }
            """;
        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitMethodNamingDiagnosticId);
    }

    [Fact]
    public async Task Strict_test_naming_with_mixed_case_later_segment_reports_s_k_t_e_s_t002()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Some_titleCase()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitMethodNamingDiagnosticId);
    }

    [Fact]
    public async Task Strict_test_naming_with_sentence_style_segments_does_not_report_s_k_t_e_s_t002()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Some_title()
                {
                }
            }
            """;
        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitMethodNamingDiagnosticId);
    }

    [Fact]
    public async Task Strict_test_naming_allows_configured_acronym_and_xunit_segments()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Reports_SKTEST004_for_xUnit_helper()
                {
                }
            }
            """;
        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitMethodNamingDiagnosticId);
    }

    [Fact]
    public async Task Strict_test_naming_allows_known_type_and_framework_segments()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Returns_ValueTask_from_HttpClient_with_OpenApi_ProblemDetails()
                {
                }

                [Fact]
                public void Renders_QuickGrid_with_EditContext_and_DataAnnotationsValidator()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitMethodNamingDiagnosticId);
    }

    [Fact]
    public async Task Test_naming_with_strict_casing_disabled_allows_title_cased_later_segments_during_migration()
    {
        // Arrange
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

        var options = ImmutableDictionary<string, string>.Empty
            .Add("sharedkernel_testing_strict_test_method_casing", " false ");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, analyzerOptions: options);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitMethodNamingDiagnosticId);
    }

    [Fact]
    public async Task Fact_attribute_form_without_underscores_reports_s_k_t_e_s_t002()
    {
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [FactAttribute]
                public void UsesLocalWarningSuppression()
                {
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitMethodNamingDiagnosticId);
    }

    [Fact]
    public async Task Required_trait_config_missing_from_test_method_reports_s_k_t_e_s_t003()
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
        var options = ImmutableDictionary<string, string>.Empty
            .Add("sharedkernel_testing_required_traits", "Category=Smoke");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, analyzerOptions: options);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitRequiredTraitDiagnosticId);
    }

    [Fact]
    public async Task Required_trait_config_already_on_method_does_not_report_s_k_t_e_s_t003()
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
        var options = ImmutableDictionary<string, string>.Empty
            .Add("sharedkernel_testing_required_traits", "Category=Smoke");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, analyzerOptions: options);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitRequiredTraitDiagnosticId);
    }

    [Fact]
    public async Task Required_trait_config_already_on_class_does_not_report_s_k_t_e_s_t003()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [Trait("Category", "Smoke")]
            public sealed class TourLoaderTests
            {
                [Fact]
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
    public async Task Required_trait_config_already_on_assembly_does_not_report_s_k_t_e_s_t003()
    {
        // Arrange
        const string source = """
            [assembly: global::Xunit.Trait("Category", "Smoke")]

            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
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
    public async Task Required_trait_config_multiple_missing_traits_reports_once_per_trait()
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
        var options = ImmutableDictionary<string, string>.Empty
            .Add("sharedkernel_testing_required_traits", "Category=Smoke;Scope=Integration");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, analyzerOptions: options);

        // Assert
        Assert.Equal(2, diagnostics.Count(static candidate => candidate.Id == XunitRequiredTraitDiagnosticId));
    }

    [Fact]
    public async Task Reused_private_static_helper_method_in_xunit_test_class_reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTourId();
                }

                [Fact]
                public void Updates_a_tour_when_the_request_is_valid()
                {
                    CreateTourId();
                }

                private static int CreateTourId()
                {
                    return 42;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Reused_internal_static_helper_method_in_xunit_test_class_reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTourId();
                }

                [Fact]
                public void Updates_a_tour_when_the_request_is_valid()
                {
                    CreateTourId();
                }

                internal static int CreateTourId()
                {
                    return 42;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Single_use_internal_static_helper_method_in_xunit_test_class_reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTourId();
                }

                internal static int CreateTourId()
                {
                    return 42;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Public_helper_method_in_xunit_test_class_reports_SKTEST004()
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

                public static int CreateTourId()
                {
                    return 42;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Private_static_helper_method_in_xunit_test_class_reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTourId();
                }

                private static int CreateTourId()
                {
                    return 42;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Private_instance_helper_method_in_xunit_test_class_reports_SKTEST004()
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

                private int CreateTourId()
                {
                    return 42;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Implicit_private_helper_method_in_xunit_test_class_reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTourId();
                }

                static int CreateTourId()
                {
                    return 42;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Private_method_in_non_test_class_does_not_report_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                private static int CreateTourId()
                {
                    return 42;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Expression_bodied_local_helper_function_in_xunit_test_method_reports_SKTEST004()
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

                    static int CreateTourId() => 42;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Block_bodied_local_helper_function_in_xunit_test_method_reports_SKTEST004()
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

                    static int CreateTourId()
                    {
                        return 42;
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Local_function_in_non_test_method_does_not_report_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public int CreateTour()
                {
                    return CreateTourId();

                    static int CreateTourId()
                    {
                        return 42;
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Private_nested_helper_class_in_xunit_test_class_reports_SKTEST004()
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

                private sealed class TourBuilder
                {
                    private static int CreateTourId()
                    {
                        return 42;
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Single(diagnostics.Where(static candidate => candidate.Id == XunitHelperMethodDiagnosticId));
    }

    [Fact]
    public async Task Implicit_private_nested_helper_class_in_xunit_test_class_reports_SKTEST004()
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

                sealed class TourBuilder
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Single(diagnostics.Where(static candidate => candidate.Id == XunitHelperMethodDiagnosticId));
    }

    [Fact]
    public async Task Private_nested_helper_record_in_xunit_test_class_reports_SKTEST004()
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

                private sealed record TourBuilder(int Id);
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Single(diagnostics.Where(static candidate => candidate.Id == XunitHelperMethodDiagnosticId));
    }

    [Fact]
    public async Task Public_nested_helper_class_in_xunit_test_class_reports_SKTEST004()
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

                public sealed class TourBuilder
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Single(diagnostics.Where(static candidate => candidate.Id == XunitHelperMethodDiagnosticId));
    }

    [Fact]
    public async Task Private_nested_class_in_non_test_class_does_not_report_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                private sealed class TourBuilder
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Disposable_lifecycle_method_does_not_report_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests : System.IDisposable
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                }

                void System.IDisposable.Dispose()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Async_lifetime_initialize_method_does_not_report_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests : IAsyncLifetime
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                }

                public global::System.Threading.Tasks.ValueTask InitializeAsync()
                {
                    return global::System.Threading.Tasks.ValueTask.CompletedTask;
                }

                public global::System.Threading.Tasks.ValueTask DisposeAsync()
                {
                    return global::System.Threading.Tasks.ValueTask.CompletedTask;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Private_dispose_helper_method_in_xunit_test_class_reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests : System.IDisposable
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    Dispose();
                }

                [Fact]
                public void Updates_a_tour_when_the_request_is_valid()
                {
                    Dispose();
                }

                private static void Dispose()
                {
                }

                void System.IDisposable.Dispose()
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Async_disposable_lifecycle_method_does_not_report_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests : System.IAsyncDisposable
            {
                [Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                }

                public System.Threading.Tasks.ValueTask DisposeAsync()
                {
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitHelperMethodDiagnosticId);
    }

    [Fact]
    public async Task Serial_collection_without_justification_reports_SKTEST005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [global::Xunit.CollectionDefinition("Serial database", DisableParallelization = true)]
            public sealed class SerialDatabaseCollection
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitSerialJustificationDiagnosticId);
    }

    [Fact]
    public async Task Serial_collection_with_justification_does_not_report_SKTEST005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [SerialTestJustification("Shared database reset must not overlap with other tests.")]
            [global::Xunit.CollectionDefinition("Serial database", DisableParallelization = true)]
            public sealed class SerialDatabaseCollection
            {
            }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            internal sealed class SerialTestJustificationAttribute(string reason) : System.Attribute
            {
                public string Reason { get; } = reason;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitSerialJustificationDiagnosticId);
    }

    [Fact]
    public async Task Serial_collection_with_whitespace_justification_reports_SKTEST005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [SerialTestJustification("   ")]
            [global::Xunit.CollectionDefinition("Serial database", DisableParallelization = true)]
            public sealed class SerialDatabaseCollection
            {
            }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            internal sealed class SerialTestJustificationAttribute(string reason) : System.Attribute
            {
                public string Reason { get; } = reason;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static candidate => candidate.Id == XunitSerialJustificationDiagnosticId);
    }

    [Fact]
    public async Task Parallel_collection_without_justification_does_not_report_SKTEST005()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [global::Xunit.CollectionDefinition("Parallel database")]
            public sealed class ParallelDatabaseCollection
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == XunitSerialJustificationDiagnosticId);
    }
}
