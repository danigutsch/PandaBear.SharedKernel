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

    [Fact]
    public async Task Reused_Private_Static_Helper_Method_In_Xunit_Test_Class_Reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTourId();
                }

                [global::Xunit.Fact]
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
    public async Task Reused_Internal_Static_Helper_Method_In_Xunit_Test_Class_Reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    CreateTourId();
                }

                [global::Xunit.Fact]
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
    public async Task Single_Use_Internal_Static_Helper_Method_In_Xunit_Test_Class_Reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
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
    public async Task Public_Helper_Method_In_Xunit_Test_Class_Reports_SKTEST004()
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
    public async Task Private_Static_Helper_Method_In_Xunit_Test_Class_Reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
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
    public async Task Private_Instance_Helper_Method_In_Xunit_Test_Class_Reports_SKTEST004()
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
    public async Task Implicit_Private_Helper_Method_In_Xunit_Test_Class_Reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
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
    public async Task Private_Method_In_Non_Test_Class_Does_Not_Report_SKTEST004()
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
    public async Task Expression_Bodied_Local_Helper_Function_In_Xunit_Test_Method_Reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
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
    public async Task Block_Bodied_Local_Helper_Function_In_Xunit_Test_Method_Reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests
            {
                [global::Xunit.Fact]
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
    public async Task Local_Function_In_Non_Test_Method_Does_Not_Report_SKTEST004()
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
    public async Task Private_Nested_Helper_Class_In_Xunit_Test_Class_Reports_SKTEST004()
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
    public async Task Implicit_Private_Nested_Helper_Class_In_Xunit_Test_Class_Reports_SKTEST004()
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
    public async Task Private_Nested_Helper_Record_In_Xunit_Test_Class_Reports_SKTEST004()
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

                private sealed record TourBuilder(int Id);
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Single(diagnostics.Where(static candidate => candidate.Id == XunitHelperMethodDiagnosticId));
    }

    [Fact]
    public async Task Public_Nested_Helper_Class_In_Xunit_Test_Class_Reports_SKTEST004()
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
    public async Task Private_Nested_Class_In_Non_Test_Class_Does_Not_Report_SKTEST004()
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
    public async Task Disposable_Lifecycle_Method_Does_Not_Report_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests : System.IDisposable
            {
                [global::Xunit.Fact]
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
    public async Task Async_Lifetime_Initialize_Method_Does_Not_Report_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests : global::Xunit.IAsyncLifetime
            {
                [global::Xunit.Fact]
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
    public async Task Private_Dispose_Helper_Method_In_Xunit_Test_Class_Reports_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests : System.IDisposable
            {
                [global::Xunit.Fact]
                public void Creates_a_tour_when_the_request_is_valid()
                {
                    Dispose();
                }

                [global::Xunit.Fact]
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
    public async Task Async_Disposable_Lifecycle_Method_Does_Not_Report_SKTEST004()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoaderTests : System.IAsyncDisposable
            {
                [global::Xunit.Fact]
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
    public async Task Serial_Collection_Without_Justification_Reports_SKTEST005()
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
    public async Task Serial_Collection_With_Justification_Does_Not_Report_SKTEST005()
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
    public async Task Serial_Collection_With_Whitespace_Justification_Reports_SKTEST005()
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
    public async Task Parallel_Collection_Without_Justification_Does_Not_Report_SKTEST005()
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
