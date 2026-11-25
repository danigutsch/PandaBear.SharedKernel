using System.Globalization;

namespace SharedKernel.Testing.Analyzers.Tests;

/// <summary>Tests the SKTEST002 built-in and configured test method name segments and the SKTEST021 configuration check.</summary>
public sealed class TestMethodNameSegmentTests
{
    private const string NamingDiagnosticId = TestingDiagnosticIds.XunitTestMethodNaming;
    private const string InvalidSegmentDiagnosticId = TestingDiagnosticIds.InvalidAdditionalAllowedSegment;

    [Fact]
    public void Task_is_the_only_built_in_entry_whose_lowercase_form_stays_allowed()
    {
        // Act
        var unforced = TestMethodNameSegments.BuiltIn.Where(static entry => !TestMethodNameSegments.ForcesCasing(entry));

        // Assert
        unforced.ShouldBe(["Task"]);
    }

    [Fact]
    public void Built_in_entries_have_one_spelling_per_concept()
    {
        // Act
        var duplicates = TestMethodNameSegments.BuiltIn
            .GroupBy(static entry => entry, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key);

        // Assert
        duplicates.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(TestMethodNameTestSource.BuiltInEntries), MemberType = typeof(TestMethodNameTestSource))]
    public async Task A_built_in_entry_in_its_listed_spelling_is_allowed(string entry)
    {
        // Arrange
        var source = TestMethodNameTestSource.Create($"Handles_the_{entry}_value");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(NamingDiagnosticId, source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(TestMethodNameTestSource.ForcedBuiltInEntries), MemberType = typeof(TestMethodNameTestSource))]
    public async Task A_lowercase_built_in_entry_is_reported_with_its_listed_spelling(string entry, string lowercase)
    {
        // Arrange
        var source = TestMethodNameTestSource.Create($"Handles_the_{lowercase}_value");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(NamingDiagnosticId, source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            $"xUnit test method 'Handles_the_{lowercase}_value' should follow the underscore naming convention; write '{lowercase}' as '{entry}'");
    }

    [Theory]
    [InlineData("Parses_the_Json_payload", "'Json' as 'JSON'")]
    [InlineData("Parses_the_Guid_value", "'Guid' as 'GUID'")]
    [InlineData("Ui_renders_the_menu", "'Ui' as 'UI'")]
    [InlineData("Sends_json_to_the_api", "'json' as 'JSON', 'api' as 'API'")]
    public async Task Mis_cased_entries_are_reported_with_every_correction(string methodName, string corrections)
    {
        // Arrange
        var source = TestMethodNameTestSource.Create(methodName);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(NamingDiagnosticId, source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            $"xUnit test method '{methodName}' should follow the underscore naming convention; write {corrections}");
    }

    [Theory]
    [InlineData("Opens_the_main_page")]
    [InlineData("Charges_the_paid_order")]
    [InlineData("Completes_the_task")]
    [InlineData("Completes_the_Task")]
    [InlineData("Task_completes_once")]
    [InlineData("XUnit_runs_the_test")]
    [InlineData("Runs_SKTEST021_checks")]
    public async Task Whole_segment_matching_and_plain_word_entries_are_not_reported(string methodName)
    {
        // Arrange
        var source = TestMethodNameTestSource.Create(methodName);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(NamingDiagnosticId, source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_title_cased_word_that_is_not_listed_has_no_correction_in_the_message()
    {
        // Arrange
        var source = TestMethodNameTestSource.Create("Some_Title");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(NamingDiagnosticId, source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            "xUnit test method 'Some_Title' should follow the underscore naming convention");
    }

    [Theory]
    [InlineData("Calls_the_BFF")]
    [InlineData("Publishes_the_IntegrationEvent")]
    [InlineData("Returns_Errors_for_the_request")]
    [InlineData("Returns_errors_for_the_request")]
    public async Task Configured_segments_are_allowed(string methodName)
    {
        // Arrange
        var source = TestMethodNameTestSource.Create(methodName);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(NamingDiagnosticId, source, TestMethodNameTestSource.Configured(" BFF, IntegrationEvent ,Errors"));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Calls_the_bff", "'bff' as 'BFF'")]
    [InlineData("Publishes_the_integrationevent", "'integrationevent' as 'IntegrationEvent'")]
    [InlineData("Stores_in_s3", "'s3' as 'S3'")]
    public async Task Configured_segments_force_their_spelling(string methodName, string correction)
    {
        // Arrange
        var source = TestMethodNameTestSource.Create(methodName);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(NamingDiagnosticId, source, TestMethodNameTestSource.Configured("BFF,IntegrationEvent,S3"));

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            $"xUnit test method '{methodName}' should follow the underscore naming convention; write {correction}");
        diagnostic.Properties[TestMethodNameSegments.AdditionalSegmentsProperty].ShouldBe("BFF,IntegrationEvent,S3");
    }

    [Theory]
    [InlineData("Calls_the_BFFs")]
    [InlineData("Calls_the_BFF_Gateway")]
    public async Task Configured_segments_do_not_relax_the_rule_for_other_segments(string methodName)
    {
        // Arrange
        var source = TestMethodNameTestSource.Create(methodName);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(NamingDiagnosticId, source, TestMethodNameTestSource.Configured("BFF"));

        // Assert
        diagnostics.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_segment_that_is_not_configured_is_reported()
    {
        // Arrange
        var source = TestMethodNameTestSource.Create("Calls_the_BFF");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(NamingDiagnosticId, source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Properties.ContainsKey(TestMethodNameSegments.AdditionalSegmentsProperty).ShouldBeFalse();
    }

    [Fact]
    public async Task Malformed_configured_entries_are_reported_once_each_and_ignored()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class GatewayTests
            {
                [Fact]
                public void Calls_the_BFF()
                {
                }

                [Fact]
                public void Calls_the_Integration_Event()
                {
                }

                [Fact]
                public void Parses_the_Json_payload()
                {
                }
            }
            """;
        var options = TestMethodNameTestSource.Configured("BFF, Integration_Event, , Foo-Bar, 2FA, Json");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, analyzerOptions: options);

        // Assert
        diagnostics.Where(static diagnostic => diagnostic.Id == InvalidSegmentDiagnosticId)
            .Select(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))
            .Order(StringComparer.Ordinal)
            .ToArray()
            .ShouldBe(
            [
                "sharedkernel_testing_additional_allowed_segments entry '' is ignored: an entry must start with a letter, contain only letters and digits, and not repeat a built-in segment",
                "sharedkernel_testing_additional_allowed_segments entry '2FA' is ignored: an entry must start with a letter, contain only letters and digits, and not repeat a built-in segment",
                "sharedkernel_testing_additional_allowed_segments entry 'Foo-Bar' is ignored: an entry must start with a letter, contain only letters and digits, and not repeat a built-in segment",
                "sharedkernel_testing_additional_allowed_segments entry 'Integration_Event' is ignored: an entry must start with a letter, contain only letters and digits, and not repeat a built-in segment",
                "sharedkernel_testing_additional_allowed_segments entry 'Json' is ignored: an entry must start with a letter, contain only letters and digits, and not repeat a built-in segment",
            ]);
        diagnostics.Where(static diagnostic => diagnostic.Id == NamingDiagnosticId)
            .Select(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))
            .Order(StringComparer.Ordinal)
            .ToArray()
            .ShouldBe(
            [
                "xUnit test method 'Calls_the_Integration_Event' should follow the underscore naming convention",
                "xUnit test method 'Parses_the_Json_payload' should follow the underscore naming convention; write 'Json' as 'JSON'",
            ]);
    }

    [Theory]
    [InlineData("tests/Gateway/GatewayTests.cs", false)]
    [InlineData("tests/Catalog/CatalogTests.cs", true)]
    public async Task Configured_segments_apply_only_to_the_files_their_section_covers(string path, bool reported)
    {
        // Arrange
        var compilation = AnalyzerTestHarness.CreateCompilation(TestMethodNameTestSource.Create("Calls_the_BFF"), path: path);
        var optionsProvider = new PathScopedAnalyzerConfigOptionsProvider("tests/Gateway/", TestMethodNameTestSource.Configured("BFF"));

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(compilation, optionsProvider);

        // Assert
        diagnostics.Any(static diagnostic => diagnostic.Id == NamingDiagnosticId).ShouldBe(reported);
    }
}
