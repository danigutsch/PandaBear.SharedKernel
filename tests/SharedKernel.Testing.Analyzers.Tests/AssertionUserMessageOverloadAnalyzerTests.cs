using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Testing.Analyzers.Tests;

public sealed class AssertionUserMessageOverloadAnalyzerTests
{
    private const string DiagnosticId = "SKTEST017";
    private const string MissingOverload = "' has no overload that takes 'string userMessage'";

    [Fact]
    public async Task SKTEST017_is_disabled_by_default()
    {
        // Arrange
        var source = AssertionUserMessageOverloadTestSource.Create("public static void ShouldBeReady(this int actual) { }");
        var analyzer = new SharedKernelTestingAnalyzer();

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);
        var descriptor = analyzer.SupportedDiagnostics.ShouldHaveSingleItem(static candidate => candidate.Id == DiagnosticId);

        // Assert
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == DiagnosticId);
        descriptor.IsEnabledByDefault.ShouldBeFalse();
        descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Warning);
        descriptor.Category.ShouldBe("Testing");
        descriptor.HelpLinkUri.ShouldEndWith("#sktest017", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("public static void ShouldBeReady(this int actual) { }", "ShouldBeReady", "(int)")]
    [InlineData("public static T ShouldBeKind<T>(this object? actual) where T : class => (T)actual!;", "ShouldBeKind", "<T>(object?)")]
    [InlineData("public static void ShouldBeAmong<T>(this T actual, params T[] expected) { }", "ShouldBeAmong", "<T>(T, params T[])")]
    [InlineData(
        "public static Task ShouldBeProblem(this string actual, string? detail = null, CancellationToken cancellationToken = default) => Task.CompletedTask;",
        "ShouldBeProblem",
        "(string, string?, CancellationToken)")]
    [InlineData("public static async Task<int> ShouldFinish(this Task<int> actual) => await actual;", "ShouldFinish", "(Task<int>)")]
    public async Task Assertion_without_a_user_message_overload_reports_SKTEST017(string member, string name, string signature)
    {
        // Arrange
        var compilation = AnalyzerTestHarness.CreateCompilation(AssertionUserMessageOverloadTestSource.Create(member));
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, ReportDiagnostic.Warn)));

        // Act
        var compilationErrors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelTestingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        compilationErrors.ShouldBeEmpty();
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(DiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe("Assertion 'SampleAssertions." + name + signature + MissingOverload);
        var text = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        text.ToString(diagnostic.Location.SourceSpan).ShouldBe(name);
    }

    [Theory]
    [InlineData("public static void ShouldBeReady(this bool actual, string? userMessage = null) { }")]
    [InlineData("public static void ShouldEqual(this int actual, int expected, string userMessage) { }")]
    [InlineData("""
        public static void ShouldEqual<T>(this T actual, T expected) { }
        public static void ShouldEqual<TValue>(this TValue actual, TValue expected, string userMessage) { }
        """)]
    [InlineData("""
        public static Task ShouldBeProblem(this string actual, string? detail = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public static Task ShouldBeProblem(this string actual, string? detail, string userMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        """)]
    [InlineData("""
        public static void ShouldBeAmong<T>(this T actual, params T[] expected) { }
        public static void ShouldBeAmong<T>(this T actual, T[] expected, string userMessage) { }
        """)]
    public async Task Covered_assertions_do_not_report_SKTEST017(string members)
    {
        // Arrange
        var compilation = AnalyzerTestHarness.CreateCompilation(AssertionUserMessageOverloadTestSource.Create(members));
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, ReportDiagnostic.Warn)));

        // Act
        var compilationErrors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelTestingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == DiagnosticId);
    }

    [Theory]
    [InlineData("", "internal static class InternalAssertions { public static void ShouldBeReady(this int actual) { } }")]
    [InlineData("internal static void ShouldBeReady(this int actual) { }")]
    [InlineData("public static void ShouldBeReady(int actual) { }")]
    [InlineData("public static void Should(this int actual) { }")]
    [InlineData("public static void Shoulder(this int actual) { }")]
    [InlineData("public static bool ShouldBeReady(this int actual) => actual > 0;")]
    [InlineData("public static bool? ShouldBeReady(this int actual) => actual > 0;")]
    [InlineData("[Obsolete(\"Use ShouldBeSet.\")] public static void ShouldBeReady(this int actual) { }")]
    [InlineData("""
        [SuppressMessage("Testing", "SKTEST017:Public assertions should have a userMessage overload", Justification = "The sample keeps its shape.")]
        public static void ShouldBeReady(this int actual) { }
        """)]
    [InlineData("public static void ShouldBeReady(this int actual) { }", "", "SampleAssertions.g.cs")]
    public async Task Methods_that_are_not_assertions_do_not_report_SKTEST017(string members, string declarations = "", string path = "TestSource.cs")
    {
        // Arrange
        var compilation = AnalyzerTestHarness.CreateCompilation(AssertionUserMessageOverloadTestSource.Create(members, declarations), path: path);
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, ReportDiagnostic.Warn)));

        // Act
        var compilationErrors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelTestingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == DiagnosticId);
    }

    [Fact]
    public async Task Misplaced_or_foreign_overloads_do_not_cover_SKTEST017()
    {
        // Arrange
        var source = AssertionUserMessageOverloadTestSource.Create(
            """
            public static Task ShouldBeProblem(this string actual, int status, CancellationToken cancellationToken) => Task.CompletedTask;
            public static Task ShouldBeProblem(this string actual, int status, CancellationToken cancellationToken, string userMessage) => Task.CompletedTask;
            public static void ShouldBeReady(this int actual) { }
            """,
            """
            public static class OtherAssertions
            {
                public static void ShouldBeReady(this int actual, string userMessage) { }
            }
            """);
        var compilation = AnalyzerTestHarness.CreateCompilation(source);
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, ReportDiagnostic.Warn)));

        // Act
        var compilationErrors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelTestingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start)
            .Select(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))
            .ShouldBe(
            [
                "Assertion 'SampleAssertions.ShouldBeProblem(string, int, CancellationToken)" + MissingOverload,
                "Assertion 'SampleAssertions.ShouldBeReady(int)" + MissingOverload,
            ]);
    }

    [Theory]
    [InlineData(
        """
        public static T ShouldFail<T>(this Action action) where T : Exception => throw new InvalidOperationException();
        public static T ShouldFail<T>(this Action action, string? paramName) where T : ArgumentException => throw new InvalidOperationException();
        """,
        "ShouldFail<T>(Action)",
        "'SampleAssertions.ShouldFail<T>(Action, string?)' already uses that signature")]
    [InlineData(
        "public static T ShouldExist<T>([NotNull] this T? actual) where T : class => actual ?? throw new InvalidOperationException();",
        "ShouldExist<T>(T?)",
        "parameter 'actual' has [NotNull], whose null state does not flow out of a lambda; write the overload by hand")]
    [InlineData("public static void ShouldMatch(this int actual, in int expected) { }", "ShouldMatch(int, in int)", "a lambda cannot capture parameter 'expected'")]
    [InlineData("public static void ShouldBeBlank(this ReadOnlySpan<char> actual) { }", "ShouldBeBlank(ReadOnlySpan<char>)", "a lambda cannot capture parameter 'actual'")]
    [InlineData("public static ValueTask ShouldComplete(this Task actual) => new(actual);", "ShouldComplete(Task)", "the userMessage helper cannot forward a 'ValueTask' result")]
    [InlineData(
        "public static IEnumerable<int> ShouldYield(this int actual) { yield return actual; }",
        "ShouldYield(int)",
        "the userMessage helper cannot forward a 'IEnumerable<int> iterator' result")]
    [InlineData("public static async void ShouldSettle(this Task actual) => await actual;", "ShouldSettle(Task)", "the userMessage helper cannot forward a 'async void' result")]
    [InlineData("public static ref int ShouldPin(this int[] actual) => ref actual[0];", "ShouldPin(int[])", "the userMessage helper cannot forward a 'ref int' result")]
    [InlineData(
        "public static ReadOnlySpan<char> ShouldTrim(this string actual) => actual.AsSpan();",
        "ShouldTrim(string)",
        "the userMessage helper cannot forward a 'ReadOnlySpan<char>' result")]
    [InlineData(
        """
        public static void ShouldInclude(this string? actual, string expected) { }
        public static void ShouldInclude(this string? actual, string expected, StringComparison comparisonType) { }
        """,
        "ShouldInclude(string?, string)",
        "forwarding to it would omit the StringComparison that CA1307 requires; forward to 'SampleAssertions.ShouldInclude(string?, string, StringComparison)' by hand")]
    [InlineData(
        "public static void ShouldBeNear(this double actual, double expected, int precision = 2) { }",
        "ShouldBeNear(double, double, int)",
        "its optional parameters would become required in the overload, which RS0027 reports on the original")]
    public async Task Blocked_assertions_report_SKTEST017_with_the_reason(string members, string signature, string reason)
    {
        // Arrange
        var expectedMessage = "Assertion 'SampleAssertions." + signature + MissingOverload + ": " + reason;
        var compilation = AnalyzerTestHarness.CreateCompilation(AssertionUserMessageOverloadTestSource.Create(members));
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(DiagnosticId, ReportDiagnostic.Warn)));

        // Act
        var compilationErrors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelTestingAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.ShouldHaveSingleItem(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture) == expectedMessage);
    }
}
