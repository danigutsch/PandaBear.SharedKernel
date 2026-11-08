using System.Globalization;

namespace PandaBear.Domain.Analyzers.Tests;

/// <summary>Tests that the domain analyzer reports reads of the ambient clock (SKDOM003) only where intended.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DomainEncapsulationCapability)]
public sealed class AmbientClockAnalyzerTests
{
    [Theory]
    [InlineData("DateTime.Now", "DateTime.Now")]
    [InlineData("DateTime.UtcNow", "DateTime.UtcNow")]
    [InlineData("DateTime.Today", "DateTime.Today")]
    [InlineData("DateTimeOffset.Now", "DateTimeOffset.Now")]
    [InlineData("DateTimeOffset.UtcNow", "DateTimeOffset.UtcNow")]
    [InlineData("System.DateTime.UtcNow", "DateTime.UtcNow")]
    [InlineData("System.DateTimeOffset.Now", "DateTimeOffset.Now")]
    public async Task Ambient_clock_member_reports_skdom003(string expression, string displayedMember)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            public sealed class Tour
            {
                public object Stamp()
                {
                    return {{expression}};
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(DomainDiagnosticIds.AmbientClock);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain($"'{displayedMember}'", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ambient_clock_inside_property_getter_reports_skdom003()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Tour
            {
                public bool IsPast => DateTime.UtcNow > new DateTime(2020, 1, 1);
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(DomainDiagnosticIds.AmbientClock);
    }

    [Fact]
    public async Task Ambient_clock_through_using_static_reports_once()
    {
        // Arrange
        const string source = """
            using static System.DateTime;

            namespace Demo;

            public sealed class Tour
            {
                public DateTime Stamp() => UtcNow;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(DomainDiagnosticIds.AmbientClock);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'DateTime.UtcNow'", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnostic_points_at_the_whole_member_access_skdom003()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Tour
            {
                public DateTime Stamp() => DateTime.UtcNow;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        var sourceText = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        sourceText.ToString(diagnostic.Location.SourceSpan).ShouldBe("DateTime.UtcNow");
    }

    [Theory]
    [InlineData("public DateTime Value() => DateTime.MinValue;")]
    [InlineData("public DateTime Value() => DateTime.MaxValue;")]
    [InlineData("public DateTimeKind Kind() => DateTimeKind.Utc;")]
    [InlineData("public DateTimeOffset Value(TimeProvider clock) => clock.GetUtcNow();")]
    [InlineData("public DateTime Value(DateTime today) => today;")]
    [InlineData("public long Value() => System.Diagnostics.Stopwatch.GetTimestamp();")]
    [InlineData("public string Value() => nameof(DateTime.UtcNow);")]
    [InlineData("public DateTime Value() => new Clock().UtcNow;")]
    [InlineData("public DateTime Value() => Clock.Today;")]
    public async Task Non_ambient_clock_usage_is_not_reported(string member)
    {
        // Arrange
        var source = $$"""
            namespace Demo;

            public sealed class Clock
            {
                public DateTime UtcNow => DateTime.MinValue;

                public static DateTime Today => DateTime.MinValue;
            }

            public sealed class Tour
            {
                {{member}}
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
