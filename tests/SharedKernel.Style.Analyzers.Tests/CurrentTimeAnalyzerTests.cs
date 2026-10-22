namespace PandaBear.Style.Analyzers.Tests;

public sealed class CurrentTimeAnalyzerTests
{
    [Theory]
    [InlineData("DateTime.Now", true)]
    [InlineData("DateTime.UtcNow", true)]
    [InlineData("DateTime.Today", true)]
    [InlineData("DateTimeOffset.Now", true)]
    [InlineData("DateTimeOffset.UtcNow", true)]
    [InlineData("new DateTime(2026, 1, 1)", false)]
    [InlineData("DateTime.Parse(\"2026-01-01\")", false)]
    [InlineData("TimeProvider.System.GetUtcNow()", false)]
    public async Task Current_time_requires_a_provider(string expression, bool expected)
    {
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics($$"""
            public class Clock { public object Read() => {{expression}}; }
            """);
        diagnostics.Any(static d => d.Id == "SKSTYLE014").ShouldBe(expected);
    }

    [Theory]
    [InlineData("TestSource.cs", true)]
    [InlineData("TestSource.g.cs", false)]
    public async Task Clock_aliases_and_static_imports_respect_generated_boundaries(string path, bool expected)
    {
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics("""
            using Clock = System.DateTimeOffset;
            using static System.DateTime;
            public class Reader { public object Read() => Clock.UtcNow; public object TodayValue() => Today; }
            """, path: path);
        diagnostics.Count(static d => d.Id == "SKSTYLE014").ShouldBe(expected ? 2 : 0);
    }

    [Fact]
    public async Task User_properties_named_like_clock_apis_are_not_reported()
    {
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics("""
            public class DateTime { public static int Now => 1; }
            public class Clock { public int Read() => DateTime.Now; }
            """);
        diagnostics.ShouldNotContain(static d => d.Id == "SKSTYLE014");
    }
}
